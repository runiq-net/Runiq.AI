namespace Runiq.Workflows;

/// <summary>
/// Workflow çalıştırmasının nihai sonucunu temsil eder.
/// </summary>
public sealed class WorkflowExecutionResult
{
    public WorkflowExecutionResult(
        WorkflowExecutionStatus status,
        IReadOnlyList<WorkflowStepExecutionResult> stepResults,
        string? finalOutput = null,
        string? errorMessage = null)
    {
        Status = status;
        StepResults = stepResults ?? throw new ArgumentNullException(nameof(stepResults));
        FinalOutput = finalOutput;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Workflow genel durumunu döner.
    /// </summary>
    public WorkflowExecutionStatus Status { get; }

    /// <summary>
    /// Çalıştırılan adımların sonuçlarını döner.
    /// </summary>
    public IReadOnlyList<WorkflowStepExecutionResult> StepResults { get; }

    /// <summary>
    /// Workflow'un ürettiği nihai çıktıyı döner.
    /// </summary>
    public string? FinalOutput { get; }

    /// <summary>
    /// Workflow başarısız olduysa hata mesajını döner.
    /// </summary>
    public string? ErrorMessage { get; }
}