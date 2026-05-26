namespace Runiq.Workflows;

/// <summary>
/// Workflow içindeki tek bir çalıştırılabilir adımı temsil eder.
/// </summary>
public sealed class WorkflowStep
{
    internal WorkflowStep(string id, Type executableType)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Workflow step id cannot be empty.", nameof(id));
        }

        Id = id.Trim();
        ExecutableType = executableType ?? throw new ArgumentNullException(nameof(executableType));
    }

    /// <summary>
    /// Workflow içindeki adım kimliğini döner.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Bu adımda çalıştırılacak agent, tool veya yürütülebilir sınıf tipini döner.
    /// </summary>
    public Type ExecutableType { get; }

    /// <summary>
    /// Adım başarılı olduğunda gidilecek sonraki adım kimliğini döner.
    /// </summary>
    public string? SuccessStepId { get; internal set; }

 
    /// <summary>
    /// Adım hata verdiğinde uygulanacak davranışı döner.
    /// </summary>
    public WorkflowFailureBehavior FailureBehavior { get; internal set; } = WorkflowFailureBehavior.Stop;

    /// <summary>
    /// Hata durumunda devam edilecek veya yönlenilecek adım kimliğini döner.
    /// </summary>
    public string? FailureStepId { get; internal set; }
}