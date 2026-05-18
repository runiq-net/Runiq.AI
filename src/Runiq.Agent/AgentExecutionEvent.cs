namespace Runiq.Agents;

/// <summary>
/// Agent çalışması sırasında üretilen stream olayını temsil eder.
/// </summary>
public sealed record AgentExecutionEvent(
    AgentExecutionEventKind Kind,
    string? Content,
    string? ToolCallId = null,
    string? ToolName = null,
    string? ArgumentsJson = null,
    string? OutputJson = null,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static AgentExecutionEvent AssistantDelta(string content)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.AssistantDelta,
            Content: content);
    }

    public static AgentExecutionEvent ToolCallStarted(
        string toolCallId,
        string toolName,
        string argumentsJson)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.ToolCallStarted,
            Content: toolName,
            ToolCallId: toolCallId,
            ToolName: toolName,
            ArgumentsJson: argumentsJson);
    }

    public static AgentExecutionEvent ToolCallCompleted(
        string toolCallId,
        string toolName,
        string outputJson)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.ToolCallCompleted,
            Content: outputJson,
            ToolCallId: toolCallId,
            ToolName: toolName,
            OutputJson: outputJson);
    }

    public static AgentExecutionEvent ToolCallFailed(
        string toolCallId,
        string toolName,
        string errorMessage,
        string? errorCode = null)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.ToolCallFailed,
            Content: errorMessage,
            ToolCallId: toolCallId,
            ToolName: toolName,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    public static AgentExecutionEvent Completed()
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.Completed,
            Content: null);
    }

    public static AgentExecutionEvent Failed(string content)
    {
        return new AgentExecutionEvent(
            Kind: AgentExecutionEventKind.Failed,
            Content: content,
            ErrorMessage: content);
    }
}

/// <summary>
/// Agent stream olay tiplerini belirtir.
/// </summary>
public enum AgentExecutionEventKind
{
    AssistantDelta = 0,
    ToolCallStarted = 1,
    ToolCallCompleted = 2,
    ToolCallFailed = 3,
    Completed = 4,
    Failed = 5
}