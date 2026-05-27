namespace Runiq.Core.Workflows;

/// <summary>
/// Dashboard tarafından görüntülenecek workflow metadata bilgisini taşır.
/// </summary>
public sealed record WorkflowMetadataDto(
    string Id,
    string Name,
    string? StartStepId,
    int StepCount,
    IReadOnlyList<WorkflowStepMetadataDto> Steps);

/// <summary>
/// Dashboard tarafından görüntülenecek workflow step metadata bilgisini taşır.
/// </summary>
public sealed record WorkflowStepMetadataDto(
    string Id,
    string AgentType,
    string AgentName,
    string? SuccessStepId,
    string FailureBehavior,
    string? FailureStepId);

/// <summary>
/// Dashboard workflow çalıştırma isteğini taşır.
/// </summary>
public sealed record WorkflowRunRequestDto(string? Input);

/// <summary>
/// Dashboard workflow çalıştırma sonucunu taşır.
/// </summary>
public sealed record WorkflowRunResponseDto(
    string WorkflowId,
    string Status,
    string? FinalOutput,
    string? ErrorMessage,
    IReadOnlyList<WorkflowStepRunResultDto> Steps);

/// <summary>
/// Dashboard workflow adım çalıştırma sonucunu taşır.
/// </summary>
public sealed record WorkflowStepRunResultDto(
    string StepId,
    string AgentName,
    string AgentType,
    string Status,
    string? Input,
    string? Output,
    string? ErrorMessage,
    IReadOnlyList<WorkflowToolCallRunResultDto> ToolCalls);

/// <summary>
/// Dashboard workflow tool çağrısı sonucunu taşır.
/// </summary>
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
