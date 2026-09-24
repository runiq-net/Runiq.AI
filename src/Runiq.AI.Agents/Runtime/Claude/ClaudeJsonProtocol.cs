using System.Text.Json;

namespace Runiq.AI.Agents.Runtime.Claude;

/// <summary>Projects Claude's print-mode JSONL without duplicating partial and complete messages.</summary>
internal sealed class ClaudeJsonProtocol(string? expectedSessionId, bool requireToolBridge = false)
{
    private bool terminal;
    private bool partialBlock;
    private bool emittedText;
    private bool toolBridgeConnected;
    private readonly HashSet<string> messages = new(StringComparer.Ordinal);
    internal string? SessionId { get; private set; }
    internal bool Completed { get; private set; }
    internal string? FailureCode { get; private set; }

    internal AgentExecutionEvent? Apply(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var type = Text(root, "type");
            if (terminal) throw new ClaudeException("ClaudeOutputInvalid");
            // Subagent text belongs to a separate conversation, not the main assistant answer.
            if (root.TryGetProperty("parent_tool_use_id", out var parent) && parent.ValueKind != JsonValueKind.Null)
                return null;
            if (type == "system" && Text(root, "subtype") == "init")
            {
                if (SessionId is not null) throw new ClaudeException("ClaudeOutputInvalid");
                ConfirmSession(root);
                if (requireToolBridge && (!root.TryGetProperty("mcp_servers", out var servers) ||
                    !servers.EnumerateArray().Any(server =>
                        server.TryGetProperty("name", out var name) && name.GetString() == "runiq_agent_tools" &&
                        server.TryGetProperty("status", out var status) && status.GetString() == "connected")))
                    throw new ClaudeException("ClaudeToolBridgeFailed");
                toolBridgeConnected = requireToolBridge;
            }
            else if (type == "result")
            {
                ConfirmSession(root);
                terminal = true;
                var subtype = Text(root, "subtype");
                Completed = !root.GetProperty("is_error").GetBoolean() && subtype == "success";
                if (Completed && requireToolBridge && !toolBridgeConnected)
                    throw new ClaudeException("ClaudeToolBridgeFailed");
                if (!Completed)
                {
                    FailureCode = ClassifyFailure(root.ToString());
                    return null;
                }
                var result = Text(root, "result", allowEmpty: true);
                // Some CLI versions return only the final result; never append it twice after streaming.
                if (!emittedText && result.Length != 0) return Emit(result);
            }
            else if (type == "stream_event")
            {
                RequireSession(root);
                var value = root.GetProperty("event");
                var eventType = Text(value, "type");
                if (eventType is "message_start" or "content_block_start") partialBlock = false;
                if (eventType == "content_block_delta")
                {
                    var delta = value.GetProperty("delta");
                    if (Text(delta, "type") == "text_delta")
                    {
                        partialBlock = true;
                        return Emit(Text(delta, "text", allowEmpty: true));
                    }
                }
            }
            else if (type == "assistant")
            {
                RequireSession(root);
                var message = root.GetProperty("message");
                // Claude emits one assistant record per block; message.id is shared by those records.
                if (!messages.Add(Text(root, "uuid"))) throw new ClaudeException("ClaudeOutputInvalid");
                var text = string.Concat(message.GetProperty("content").EnumerateArray()
                    .Where(block => Text(block, "type") == "text").Select(block => Text(block, "text", allowEmpty: true)));
                var alreadyStreamed = partialBlock;
                partialBlock = false;
                if (!alreadyStreamed && text.Length != 0) return Emit(text);
            }
            // Hook, tool, thinking and additive telemetry records are not Runiq tool executions.
            return null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new ClaudeException("ClaudeOutputInvalid");
        }
    }

    private AgentExecutionEvent Emit(string text)
    {
        emittedText |= text.Length != 0;
        return AgentExecutionEvent.AssistantDelta(text);
    }

    private void RequireSession(JsonElement root)
    {
        if (SessionId is null) throw new ClaudeException("ClaudeOutputInvalid");
        ConfirmSession(root);
    }

    private void ConfirmSession(JsonElement root)
    {
        var id = Text(root, "session_id");
        if (!Guid.TryParseExact(id, "D", out _) ||
            (expectedSessionId is not null && !string.Equals(id, expectedSessionId, StringComparison.OrdinalIgnoreCase)) ||
            (SessionId is not null && !string.Equals(id, SessionId, StringComparison.OrdinalIgnoreCase)))
            throw new ClaudeException("ClaudeSessionInvalid");
        SessionId ??= id;
    }

    private static string Text(JsonElement value, string name, bool allowEmpty = false)
    {
        var text = value.GetProperty(name).GetString();
        return text is not null && (allowEmpty || !string.IsNullOrWhiteSpace(text))
            ? text : throw new ClaudeException("ClaudeOutputInvalid");
    }

    internal static string ClassifyFailure(string diagnostic)
    {
        if (Contains("runiq_agent_tools") && Contains("failed", "could not", "timeout", "timed out"))
            return "ClaudeToolBridgeFailed";
        // Human CLI diagnostics are heuristic; raw output may contain secrets and is never surfaced.
        if (Contains("not logged in", "authentication", "unauthorized", "invalid api key", "please run /login", "login required", "oauth token"))
            return "ClaudeAuthenticationFailed";
        if (Contains("no conversation found", "no session found", "session not found")) return "ClaudeSessionNotFound";
        if (Contains("invalid configuration", "error loading", "unknown option", "invalid settings", "permission mode", "invalid value"))
            return "ClaudeConfigurationInvalid";
        return "ClaudeProcessFailed";

        bool Contains(params string[] values) => values.Any(value => diagnostic.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
