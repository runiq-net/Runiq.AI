namespace Runiq.Workflows;

/// <summary>
/// Bir workflow adımı hata verdiğinde akışın nasıl davranacağını belirtir.
/// </summary>
public enum WorkflowFailureBehavior
{
    /// <summary>
    /// Hata durumunda workflow çalışmasını başarısız olarak sonlandırır.
    /// </summary>
    Stop = 0,

    /// <summary>
    /// Hata durumunda belirtilen sonraki adıma devam eder.
    /// </summary>
    Continue = 1,

    /// <summary>
    /// Hata durumunda belirtilen fallback adıma yönlenir.
    /// </summary>
    GoTo = 2
}