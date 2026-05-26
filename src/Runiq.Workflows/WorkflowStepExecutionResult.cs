namespace Runiq.Workflows;

/// <summary>
/// Tek bir workflow adımının çalıştırma sonucunu temsil eder.
/// </summary>
public sealed class WorkflowStepExecutionResult
{
    public WorkflowStepExecutionResult(
        string stepId,
        Type agentType,
        WorkflowStepExecutionStatus status,
        string? output = null,
        string? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(stepId))
        {
            throw new ArgumentException("Workflow step id cannot be empty.", nameof(stepId));
        }

        StepId = stepId.Trim();
        AgentType = agentType ?? throw new ArgumentNullException(nameof(agentType));
        Status = status;
        Output = output;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Çalıştırılan workflow adım kimliğini döner.
    /// </summary>
    public string StepId { get; }

    /// <summary>
    /// Çalıştırılan agent tipini döner.
    /// </summary>
    public Type AgentType { get; }

    /// <summary>
    /// Adım çalıştırma durumunu döner.
    /// </summary>
    public WorkflowStepExecutionStatus Status { get; }

    /// <summary>
    /// Agent tarafından üretilen metinsel çıktıyı döner.
    /// </summary>
    public string? Output { get; }

    /// <summary>
    /// Adım hata verdiyse hata mesajını döner.
    /// </summary>
    public string? ErrorMessage { get; }
}