using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core.Agents;

namespace Runiq.AI.Core.Tests.Agents;

public sealed class AgentChatStreamEventMapperTests
{
    [Fact]
    public void FromExecutionEvent_ShouldMapAssistantDelta()
    {
        // Assistant delta olayinin Dashboard stream DTO formatina dogru çevrildigini dogrular.
        var executionEvent = AgentExecutionEvent.AssistantDelta("Hello");

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Equal("assistant_delta", streamEvent.Type);
        Assert.Equal("Hello", streamEvent.Content);
        Assert.Null(streamEvent.ToolCallId);
        Assert.Null(streamEvent.ToolName);
        Assert.Null(streamEvent.ArgumentsJson);
        Assert.Null(streamEvent.OutputJson);
        Assert.Null(streamEvent.ErrorCode);
        Assert.Null(streamEvent.ErrorMessage);
    }

    [Fact]
    public void FromExecutionEvent_ShouldMapToolCallStarted()
    {
        // Tool call started olayinda tool id, tool adi ve argumentsJson alanlarinin korundugunu dogrular.
        var executionEvent = AgentExecutionEvent.ToolCallStarted(
            toolCallId: "call_123",
            toolName: "weather",
            argumentsJson: """{"city":"Istanbul"}""");

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Equal("tool_call_started", streamEvent.Type);
        Assert.Equal("weather", streamEvent.Content);
        Assert.Equal("call_123", streamEvent.ToolCallId);
        Assert.Equal("weather", streamEvent.ToolName);
        Assert.Equal("""{"city":"Istanbul"}""", streamEvent.ArgumentsJson);
        Assert.Null(streamEvent.OutputJson);
        Assert.Null(streamEvent.ErrorCode);
        Assert.Null(streamEvent.ErrorMessage);
    }

    [Fact]
    public void FromExecutionEvent_ShouldMapToolCallCompleted()
    {
        // Tool call completed olayinda outputJson alaninin Dashboard DTO'suna tasindigini dogrular.
        var executionEvent = AgentExecutionEvent.ToolCallCompleted(
            toolCallId: "call_123",
            toolName: "weather",
            outputJson: """{"temperatureCelsius":23}""");

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Equal("tool_call_completed", streamEvent.Type);
        Assert.Equal("""{"temperatureCelsius":23}""", streamEvent.Content);
        Assert.Equal("call_123", streamEvent.ToolCallId);
        Assert.Equal("weather", streamEvent.ToolName);
        Assert.Null(streamEvent.ArgumentsJson);
        Assert.Equal("""{"temperatureCelsius":23}""", streamEvent.OutputJson);
        Assert.Null(streamEvent.ErrorCode);
        Assert.Null(streamEvent.ErrorMessage);
    }

    [Fact]
    public void FromExecutionEvent_ShouldMapToolCallFailed()
    {
        // Tool call failed olayinda hata kodu ve mesajinin Dashboard DTO'suna tasindigini dogrular.
        var executionEvent = AgentExecutionEvent.ToolCallFailed(
            toolCallId: "call_123",
            toolName: "weather",
            errorMessage: "Tool failed.",
            errorCode: "ToolExecutionFailed");

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Equal("tool_call_failed", streamEvent.Type);
        Assert.Equal("Tool failed.", streamEvent.Content);
        Assert.Equal("call_123", streamEvent.ToolCallId);
        Assert.Equal("weather", streamEvent.ToolName);
        Assert.Null(streamEvent.ArgumentsJson);
        Assert.Null(streamEvent.OutputJson);
        Assert.Equal("ToolExecutionFailed", streamEvent.ErrorCode);
        Assert.Equal("Tool failed.", streamEvent.ErrorMessage);
    }

    [Fact]
    public void FromExecutionEvent_ShouldMapCompleted()
    {
        // Completed olayinin stream kapanis sinyali olarak dogru tipe çevrildigini dogrular.
        var executionEvent = AgentExecutionEvent.Completed();

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Equal("completed", streamEvent.Type);
        Assert.Null(streamEvent.Content);
        Assert.Null(streamEvent.ToolCallId);
        Assert.Null(streamEvent.ToolName);
        Assert.Null(streamEvent.ArgumentsJson);
        Assert.Null(streamEvent.OutputJson);
        Assert.Null(streamEvent.ErrorCode);
        Assert.Null(streamEvent.ErrorMessage);
    }

    [Fact]
    public void FromExecutionEvent_ShouldMapFailed()
    {
        // Genel agent failure olayinda hata kodu ve mesajinin Dashboard DTO'suna tasindigini dogrular.
        var executionEvent = AgentExecutionEvent.Failed(
            errorMessage: "Agent failed.",
            errorCode: "AgentExecutionFailed");

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Equal("failed", streamEvent.Type);
        Assert.Equal("Agent failed.", streamEvent.Content);
        Assert.Null(streamEvent.ToolCallId);
        Assert.Null(streamEvent.ToolName);
        Assert.Null(streamEvent.ArgumentsJson);
        Assert.Null(streamEvent.OutputJson);
        Assert.Equal("AgentExecutionFailed", streamEvent.ErrorCode);
        Assert.Equal("Agent failed.", streamEvent.ErrorMessage);
    }

    [Fact]
    // Ensures terminal stream events carry structured RAG policy metadata without answer-text parsing.
    public void FromExecutionEvent_ShouldMapRagPolicyOutcome()
    {
        var metadata = new AgentRagExecutionMetadata(
            RagExecutionMode.Required,
            hasAcceptedContext: false,
            RagNoContextBehavior.ReturnNotFound,
            RagNoContextReason.NoResults,
            modelInvocationSkipped: true,
            isAnswerGrounded: false,
            [],
            [],
            []);
        var executionEvent = AgentExecutionEvent.Completed(metadata);

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Same(metadata, streamEvent.Rag);
    }

    [Fact]
    public void FromExecutionEvent_ShouldThrowArgumentNullException_WhenExecutionEventIsNull()
    {
        // Null execution event degerinin sessizce map edilmedigini dogrular.
        Assert.Throws<ArgumentNullException>(
            () => AgentChatStreamEventMapper.FromExecutionEvent(null!));
    }

}

