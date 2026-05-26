namespace Runiq.Workflows;

/// <summary>
/// Workflow çalıştırma sonucunun genel durumunu belirtir.
/// </summary>
public enum WorkflowExecutionStatus
{
    /// <summary>
    /// Workflow başarıyla tamamlandı.
    /// </summary>
    Completed = 0,

    /// <summary>
    /// Workflow hata nedeniyle sonlandı.
    /// </summary>
    Failed = 1
}