using Runiq.AI.Agents;

namespace Runiq.AI.Core.Agents;

/// <summary>
/// Studio üzerinden çalistirilan agent chat cevabini temsil eder.
/// </summary>
public sealed record AgentChatResponse(
    bool IsSuccess,
    string? Message,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<AgentChatExecutionStepResponse> Steps);

/// <summary>
/// Studio response içinde gösterilecek agent execution adimini temsil eder.
/// </summary>
public sealed record AgentChatExecutionStepResponse(
    int Index,
    string Kind,
    string? Content,
    string? ToolCallId,
    string? ToolName,
    string? ArgumentsJson,
    string? OutputJson,
    string? ErrorCode,
    string? ErrorMessage,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt)
{
    /// <summary>
    /// Framework execution step modelinden API response modeline dönüsüm yapar.
    /// </summary>
    public static AgentChatExecutionStepResponse FromExecutionStep(AgentExecutionStep step)
    {
        return new AgentChatExecutionStepResponse(
            Index: step.Index,
            Kind: ToKindValue(step.Kind),
            Content: step.Content,
            ToolCallId: step.ToolCallId,
            ToolName: step.ToolName,
            ArgumentsJson: step.ArgumentsJson,
            OutputJson: step.OutputJson,
            ErrorCode: step.ErrorCode,
            ErrorMessage: step.ErrorMessage,
            Status: ToStatusValue(step.Status),
            StartedAt: step.StartedAt,
            CompletedAt: step.CompletedAt);
    }

    private static string ToKindValue(AgentExecutionStepKind kind)
    {
        return kind switch
        {
            AgentExecutionStepKind.ToolCall => "tool_call",
            AgentExecutionStepKind.FinalAnswer => "final_answer",
            AgentExecutionStepKind.Error => "error",
            _ => "unknown"
        };
    }

    private static string ToStatusValue(AgentExecutionStepStatus status)
    {
        return status switch
        {
            AgentExecutionStepStatus.Running => "running",
            AgentExecutionStepStatus.Completed => "completed",
            AgentExecutionStepStatus.Failed => "failed",
            _ => "unknown"
        };
    }
}
