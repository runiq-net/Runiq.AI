namespace Runiq.Core.Workflows;

public sealed record WorkflowMetadataDto(
    string Id,
    string Name,
    string? StartStepId,
    int StepCount,
    IReadOnlyList<WorkflowStepMetadataDto> Steps);

public sealed record WorkflowStepMetadataDto(
    string Id,
    string AgentType,
    string AgentName,
    string? SuccessStepId,
    string FailureBehavior,
    string? FailureStepId);

public sealed record WorkflowRunRequestDto(string? Input);

public sealed record WorkflowRunResponseDto(
    string WorkflowId,
    string Status,
    string? FinalOutput,
    string? ErrorMessage,
    IReadOnlyList<WorkflowStepRunResultDto> Steps);

public sealed record WorkflowStepRunResultDto(
    string StepId,
    string AgentName,
    string AgentType,
    string Status,
    string? Input,
    string? Output,
    string? ErrorMessage,
    IReadOnlyList<WorkflowToolCallRunResultDto> ToolCalls);

public sealed record WorkflowToolCallRunResultDto(
    string? ToolCallId,
    string? ToolName,
    string Status,
    string? ArgumentsJson,
    string? OutputJson,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    long? DurationMs);
