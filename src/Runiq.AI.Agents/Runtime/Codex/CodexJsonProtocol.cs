using System.Text.Json;

namespace Runiq.AI.Agents.Runtime.Codex;

/// <summary>Validates one exec JSONL turn and projects supported items without inventing token deltas.</summary>
internal sealed class CodexJsonProtocol(string? expectedSessionId)
{
    private readonly HashSet<string> completedMessages = new(StringComparer.Ordinal);
    private bool started;
    private bool terminal;
    internal string? SessionId { get; private set; }
    internal bool Completed { get; private set; }
    internal string? FailureCode { get; private set; }

    internal AgentExecutionEvent? Apply(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var type = RequiredString(root, "type");
            if (terminal) throw new CodexException("CodexOutputInvalid");
            switch (type)
            {
                case "thread.started":
                    var id = RequiredString(root, "thread_id");
                    if (SessionId is not null || !Guid.TryParseExact(id, "D", out _) ||
                        (expectedSessionId is not null && !string.Equals(id, expectedSessionId, StringComparison.OrdinalIgnoreCase)))
                        throw new CodexException("CodexSessionInvalid");
                    SessionId = id;
                    break;
                case "turn.started":
                    if (SessionId is null || started) throw new CodexException("CodexOutputInvalid");
                    started = true;
                    break;
                case "turn.completed":
                    if (!started) throw new CodexException("CodexOutputInvalid");
                    terminal = Completed = true;
                    break;
                case "turn.failed":
                    FailureCode = ClassifyFailure(RequiredString(root.GetProperty("error"), "message"));
                    terminal = true;
                    break;
                case "error":
                    // Exec can emit transient errors before retrying; only turn.failed or exit decides failure.
                    FailureCode = ClassifyFailure(RequiredString(root, "message"));
                    break;
                case "item.started":
                case "item.updated":
                case "item.completed":
                    if (!started) throw new CodexException("CodexOutputInvalid");
                    var item = root.GetProperty("item");
                    var itemType = RequiredString(item, "type");
                    var itemId = RequiredString(item, "id");
                    if (itemType == "agent_message" && type == "item.completed")
                    {
                        if (!completedMessages.Add(itemId)) throw new CodexException("CodexOutputInvalid");
                        return AgentExecutionEvent.AssistantDelta(RequiredString(item, "text", allowEmpty: true));
                    }
                    // Native CLI tool items are not Runiq tool invocations; do not misrepresent their ownership.
                    break;
                default:
                    // Additive telemetry events are ignored; success still requires a valid full turn.
                    break;
            }
            return null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new CodexException("CodexOutputInvalid");
        }
    }

    private static string RequiredString(JsonElement value, string property, bool allowEmpty = false)
    {
        var text = value.GetProperty(property).GetString();
        return text is not null && (allowEmpty || !string.IsNullOrWhiteSpace(text))
            ? text : throw new CodexException("CodexOutputInvalid");
    }

    internal static string ClassifyFailure(string diagnostic)
    {
        // CLI exec has no stable typed auth/config error codes. Match only known diagnostics, never expose raw text.
        if (Contains("not logged in", "authentication", "unauthorized", "401", "login required", "refresh token"))
            return "CodexAuthenticationFailed";
        if (Contains("config.toml", "error loading config", "invalid configuration", "unexpected argument",
                "not inside a trusted directory", "not a git repository", "sandbox"))
            return "CodexConfigurationInvalid";
        if (Contains("no session found", "session not found", "thread not found"))
            return "CodexSessionNotFound";
        return "CodexProcessFailed";

        bool Contains(params string[] values) => values.Any(value => diagnostic.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
