using Runiq.AI.Agents;
using System.Text.Json.Serialization;
using System.Text.Json;
using Runiq.AI.Agents.Runtime;

namespace Runiq.AI.Core.Agents;

/// <summary>
/// Represents an Agent Chat result with legacy output fields and optional runtime metadata.
/// </summary>
public sealed record AgentChatResponse(
    bool IsSuccess,
    string? Message,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<AgentChatExecutionStepResponse> Steps)
{
    /// <summary>Gets the runtime invocation identifier, or null when rejected before execution.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public string? RunId { get; internal init; }

    /// <summary>Gets the agent definition identifier associated with this run.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public string? AgentId { get; internal init; }

    /// <summary>Gets the UTC run start time, or null when no runtime metadata is available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public DateTimeOffset? StartedAt { get; internal init; }

    /// <summary>Gets the UTC terminal transition time, or null when no runtime metadata is available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public DateTimeOffset? EndedAt { get; internal init; }

    /// <summary>Gets the terminal run status; cancellation propagates as an exception instead of a response.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(JsonStringEnumConverter<AgentRunStatus>))]
    [JsonInclude]
    public AgentRunStatus? Status { get; internal init; }

    /// <summary>Gets explicit executor JSON backed by the execution result's independently owned data.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public JsonElement? StructuredOutput { get; internal init; }

    /// <summary>
    /// Gets or initializes the structured RAG policy outcome, or null when RAG was not configured.
    /// </summary>
    [JsonIgnore]
    public AgentRagExecutionMetadata? Rag { get; init; }

    /// <summary>Gets content-free completed retrieval evidence for this response.</summary>
    public IReadOnlyList<AgentChatRagSearchEvent>? GroundingEvidence { get; init; }
    /// <summary>Gets validated citations for this response.</summary>
    public IReadOnlyList<AgentCitation>? Citations { get; init; }
    /// <summary>Gets the content-free readiness outcome when execution was blocked before retrieval.</summary>
    public AgentChatRagSearchEvent? RagReadiness { get; init; }
}

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
