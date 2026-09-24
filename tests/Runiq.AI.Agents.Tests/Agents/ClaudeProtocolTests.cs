using System.Text.Json;
using Runiq.AI.Agents.Runtime.Claude;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class ClaudeProtocolTests
{
    private const string Session = "0199a213-81c0-7800-8aa1-bbab2a035a53";

    [Theory]
    [InlineData("failed")]
    [InlineData("pending")]
    [InlineData("needs-auth")]
    // Verifies an attached Runiq bridge must be connected before Claude can proceed with a turn.
    public void RequiredToolBridge_RejectsDisconnectedServer(string status)
    {
        var protocol = new ClaudeJsonProtocol(null, requireToolBridge: true);
        var error = Assert.Throws<ClaudeException>(() => protocol.Apply(JsonSerializer.Serialize(new
        {
            type = "system", subtype = "init", session_id = Session,
            mcp_servers = new[] { new { name = "runiq_agent_tools", status } }
        })));
        Assert.Equal("ClaudeToolBridgeFailed", error.Code);
    }

    [Fact]
    // Verifies omitted server metadata cannot silently disable agent tools, while tool-free runs remain compatible.
    public void RequiredToolBridge_RejectsMissingServerMetadata()
    {
        var init = JsonSerializer.Serialize(new { type = "system", subtype = "init", session_id = Session });
        Assert.Equal("ClaudeToolBridgeFailed", Assert.Throws<ClaudeException>(() => new ClaudeJsonProtocol(null, true).Apply(init)).Code);
        Assert.Null(new ClaudeJsonProtocol(null).Apply(init));
        var result = JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, session_id = Session, result = "hello" });
        Assert.Equal("ClaudeToolBridgeFailed", Assert.Throws<ClaudeException>(() => new ClaudeJsonProtocol(null, true).Apply(result)).Code);
    }

    [Fact]
    // Verifies partial text is delivered immediately and complete assistant/result messages do not duplicate it.
    public void Streaming_MultipleMessagesAndToolBlocksPreserveTextExactlyOnce()
    {
        var protocol = Initialized();
        Assert.Null(protocol.Apply(Event(new { type = "message_start" })));
        Assert.Equal("hel", protocol.Apply(Event(new { type = "content_block_delta", delta = new { type = "text_delta", text = "hel" } }))!.Content);
        Assert.Equal("lo", protocol.Apply(Event(new { type = "content_block_delta", delta = new { type = "text_delta", text = "lo" } }))!.Content);
        Assert.Null(protocol.Apply(Event(new { type = "content_block_delta", delta = new { type = "input_json_delta", partial_json = "{}" } })));
        Assert.Null(protocol.Apply(Assistant("m1", "hello")));
        Assert.Null(protocol.Apply(Event(new { type = "content_block_stop" })));
        Assert.Null(protocol.Apply(Event(new { type = "content_block_start" })));
        Assert.Null(protocol.Apply(JsonSerializer.Serialize(new { type = "assistant", uuid = "tool-record", session_id = Session,
            message = new { id = "m1", content = new[] { new { type = "tool_use", id = "tool-1", name = "Read" } } } })));
        Assert.Null(protocol.Apply(Event(new { type = "content_block_stop" })));
        Assert.Null(protocol.Apply(Event(new { type = "message_stop" })));
        Assert.Null(protocol.Apply(Event(new { type = "message_start" })));
        Assert.Equal("done", protocol.Apply(Assistant("m1", "done"))!.Content);
        Assert.Null(protocol.Apply(Result("done")));
        Assert.True(protocol.Completed);
    }

    [Fact]
    // Verifies subagent messages, hooks and thinking remain outside the main assistant answer.
    public void Streaming_IgnoresSubagentsAndTelemetry()
    {
        var protocol = Initialized();
        Assert.Null(protocol.Apply(JsonSerializer.Serialize(new { type = "assistant", parent_tool_use_id = "tool-1" })));
        Assert.Null(protocol.Apply(JsonSerializer.Serialize(new { type = "system", subtype = "hook_progress" })));
        Assert.Null(protocol.Apply(Event(new { type = "content_block_delta", delta = new { type = "thinking_delta", thinking = "private" } })));
        Assert.Equal("answer", protocol.Apply(Result("answer"))!.Content);
    }

    [Theory]
    [InlineData("not logged in", "ClaudeAuthenticationFailed")]
    [InlineData("invalid settings", "ClaudeConfigurationInvalid")]
    [InlineData("No conversation found", "ClaudeSessionNotFound")]
    [InlineData("max turns exceeded", "ClaudeProcessFailed")]
    // Verifies a terminal error remains a failure even when the OS reports successful exit.
    public void ResultErrors_AreClassified(string diagnostic, string expected)
    {
        var protocol = Initialized();
        Assert.Null(protocol.Apply(JsonSerializer.Serialize(new { type = "result", subtype = "error_during_execution", is_error = true, errors = new[] { diagnostic }, session_id = Session })));
        Assert.False(protocol.Completed);
        Assert.Equal(expected, protocol.FailureCode);
    }

    [Theory]
    [InlineData("bad-id")]
    [InlineData("0199a213-81c0-7800-8aa1-bbab2a035a54")]
    // Verifies every main-conversation record retains the confirmed session identity.
    public void Protocol_RejectsSessionChanges(string session)
    {
        var protocol = Initialized();
        var line = JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = "", session_id = session });
        Assert.Equal("ClaudeSessionInvalid", Assert.Throws<ClaudeException>(() => protocol.Apply(line)).Code);
    }

    [Theory]
    [InlineData("{\"type\":\"assistant\",\"message\":{}}")]
    [InlineData("{\"type\":\"stream_event\",\"session_id\":null}")]
    [InlineData("[]")]
    // Verifies invalid record shapes fail deterministically instead of escaping as JSON exceptions.
    public void Protocol_RejectsMalformedShapes(string line)
        => Assert.Throws<ClaudeException>(() => Initialized().Apply(line));

    private static ClaudeJsonProtocol Initialized()
    {
        var protocol = new ClaudeJsonProtocol(Session);
        protocol.Apply(JsonSerializer.Serialize(new { type = "system", subtype = "init", session_id = Session }));
        return protocol;
    }

    private static string Event(object value) => JsonSerializer.Serialize(new { type = "stream_event", session_id = Session, @event = value });
    private static string Assistant(string id, string text) => JsonSerializer.Serialize(new { type = "assistant", uuid = Guid.NewGuid().ToString(), session_id = Session, message = new { id, content = new[] { new { type = "text", text } } } });
    private static string Result(string text) => JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = text, session_id = Session });
}
