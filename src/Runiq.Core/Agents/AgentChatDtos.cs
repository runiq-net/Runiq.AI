using System.Text.Json.Serialization;
using Runiq.Agents;

namespace Runiq.Core.Agents;

/// <summary>
/// Studio üzerinden agent'a gönderilen chat isteğini temsil eder.
/// </summary>
public sealed record AgentChatRequest(string Message);

/// <summary>
/// Studio üzerinden çalıştırılan agent chat cevabını temsil eder.
/// </summary>
public sealed record AgentChatResponse(
    bool IsSuccess,
    string? Message,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
/// Dashboard canlı chat ekranına gönderilen SSE olayını temsil eder.
/// </summary>
public sealed record AgentChatStreamEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("toolCallId")] string? ToolCallId = null,
    [property: JsonPropertyName("toolName")] string? ToolName = null,
    [property: JsonPropertyName("argumentsJson")] string? ArgumentsJson = null,
    [property: JsonPropertyName("outputJson")] string? OutputJson = null,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null);

/// <summary>
/// Agent execution olaylarını Dashboard'un beklediği stream DTO formatına çevirir.
/// </summary>
internal static class AgentChatStreamEventMapper
{
    public static AgentChatStreamEvent FromExecutionEvent(AgentExecutionEvent executionEvent)
    {
        ArgumentNullException.ThrowIfNull(executionEvent);

        return executionEvent.Kind switch
        {
            AgentExecutionEventKind.AssistantDelta => new AgentChatStreamEvent(
                Type: "assistant_delta",
                Content: executionEvent.Content),

            AgentExecutionEventKind.ToolCallStarted => new AgentChatStreamEvent(
                Type: "tool_call_started",
                Content: executionEvent.Content,
                ToolCallId: executionEvent.ToolCallId,
                ToolName: executionEvent.ToolName,
                ArgumentsJson: executionEvent.ArgumentsJson),

            AgentExecutionEventKind.ToolCallCompleted => new AgentChatStreamEvent(
                Type: "tool_call_completed",
                Content: executionEvent.Content,
                ToolCallId: executionEvent.ToolCallId,
                ToolName: executionEvent.ToolName,
                OutputJson: executionEvent.OutputJson),

            AgentExecutionEventKind.ToolCallFailed => new AgentChatStreamEvent(
                Type: "tool_call_failed",
                Content: executionEvent.Content,
                ToolCallId: executionEvent.ToolCallId,
                ToolName: executionEvent.ToolName,
                ErrorCode: executionEvent.ErrorCode,
                ErrorMessage: executionEvent.ErrorMessage),

            AgentExecutionEventKind.Completed => new AgentChatStreamEvent(
                Type: "completed",
                Content: null),

            AgentExecutionEventKind.Failed => new AgentChatStreamEvent(
                Type: "failed",
                Content: executionEvent.Content,
                ErrorCode: executionEvent.ErrorCode,
                ErrorMessage: executionEvent.ErrorMessage),

            _ => new AgentChatStreamEvent(
                Type: "failed",
                Content: $"Unsupported agent execution event kind: {executionEvent.Kind}.",
                ErrorMessage: $"Unsupported agent execution event kind: {executionEvent.Kind}.")
        };
    }
}