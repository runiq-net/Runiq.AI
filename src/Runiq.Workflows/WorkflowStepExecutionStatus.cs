namespace Runiq.Workflows;

/// <summary>
/// Workflow adımı çalıştırma durumunu belirtir.
/// </summary>
public enum WorkflowStepExecutionStatus
{
    /// <summary>
    /// Adım başarıyla tamamlandı.
    /// </summary>
    Completed = 0,

    /// <summary>
    /// Adım hata ile tamamlandı.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// Adım atlandı.
    /// </summary>
    Skipped = 2
}