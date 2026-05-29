namespace Runiq.Workflows.Models;

/// <summary>
/// Captures trace details for a tool call made by an agent step.
/// </summary>
public sealed class ToolCallRunResult
{
    public ToolCallRunResult(
        string? toolCallId,
        string? toolName,
        ToolCallRunStatus status,
        string? argumentsJson = null,
        string? outputJson = null,
        string? errorCode = null,
        string? errorMessage = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null)
    {
        ToolCallId = toolCallId;
        ToolName = toolName;
        Status = status;
        ArgumentsJson = argumentsJson;
        OutputJson = outputJson;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }

    public string? ToolCallId { get; }

    public string? ToolName { get; }

    public ToolCallRunStatus Status { get; }

    public string? ArgumentsJson { get; }

    public string? OutputJson { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }

    public DateTimeOffset? StartedAt { get; }

    public DateTimeOffset? CompletedAt { get; }

    public long? DurationMs =>
        StartedAt is null || CompletedAt is null
            ? null
            : (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds;
}
