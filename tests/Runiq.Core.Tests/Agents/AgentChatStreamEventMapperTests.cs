using Runiq.Agents;
using Runiq.Core.Agents;

namespace Runiq.Core.Tests.Agents;

public sealed class AgentChatStreamEventMapperTests
{
    [Fact]
    public void FromExecutionEvent_ShouldMapAssistantDelta()
    {
        // Assistant delta olayının Dashboard stream DTO formatına doğru çevrildiğini doğrular.
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
        // Tool call started olayında tool id, tool adı ve argumentsJson alanlarının korunduğunu doğrular.
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
        // Tool call completed olayında outputJson alanının Dashboard DTO'suna taşındığını doğrular.
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
        // Tool call failed olayında hata kodu ve mesajının Dashboard DTO'suna taşındığını doğrular.
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
        // Completed olayının stream kapanış sinyali olarak doğru tipe çevrildiğini doğrular.
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
        // Genel agent failure olayında hata kodu ve mesajının Dashboard DTO'suna taşındığını doğrular.
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
    public void FromExecutionEvent_ShouldThrowArgumentNullException_WhenExecutionEventIsNull()
    {
        // Null execution event değerinin sessizce map edilmediğini doğrular.
        Assert.Throws<ArgumentNullException>(
            () => AgentChatStreamEventMapper.FromExecutionEvent(null!));
    }
}