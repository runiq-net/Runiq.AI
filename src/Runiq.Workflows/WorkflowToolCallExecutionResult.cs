namespace Runiq.Workflows;

/// <summary>
/// Workflow agent adımı içinde çalıştırılan tek bir tool çağrısının trace bilgisini temsil eder.
/// </summary>
public sealed class WorkflowToolCallExecutionResult
{
    public WorkflowToolCallExecutionResult(
        string? toolCallId,
        string? toolName,
        WorkflowToolCallExecutionStatus status,
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

    /// <summary>
    /// Model tarafından üretilen tool çağrısı kimliğini döner.
    /// </summary>
    public string? ToolCallId { get; }

    /// <summary>
    /// Çalıştırılan tool adını döner.
    /// </summary>
    public string? ToolName { get; }

    /// <summary>
    /// Tool çağrısı durumunu döner.
    /// </summary>
    public WorkflowToolCallExecutionStatus Status { get; }

    /// <summary>
    /// Tool çağrısı argümanlarını JSON olarak döner.
    /// </summary>
    public string? ArgumentsJson { get; }

    /// <summary>
    /// Tool çıktısını JSON olarak döner.
    /// </summary>
    public string? OutputJson { get; }

    /// <summary>
    /// Tool hatası varsa hata kodunu döner.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Tool hatası varsa hata mesajını döner.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Tool çağrısının başladığı zamanı döner.
    /// </summary>
    public DateTimeOffset? StartedAt { get; }

    /// <summary>
    /// Tool çağrısının tamamlandığı zamanı döner.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; }

    /// <summary>
    /// Tool çağrısının süresini milisaniye olarak döner.
    /// </summary>
    public long? DurationMs =>
        StartedAt is null || CompletedAt is null
            ? null
            : (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds;
}

/// <summary>
/// Workflow tool çağrısı çalışma durumlarını belirtir.
/// </summary>
public enum WorkflowToolCallExecutionStatus
{
    /// <summary>
    /// Tool çağrısının çalışmakta olduğunu belirtir.
    /// </summary>
    Running = 0,

    /// <summary>
    /// Tool çağrısının başarıyla tamamlandığını belirtir.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// Tool çağrısının hata ile tamamlandığını belirtir.
    /// </summary>
    Failed = 2
}
