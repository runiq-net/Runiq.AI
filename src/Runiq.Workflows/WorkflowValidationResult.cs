namespace Runiq.Workflows;

/// <summary>
/// Workflow doğrulama sonucunu temsil eder.
/// </summary>
public sealed class WorkflowValidationResult
{
    private WorkflowValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    /// <summary>
    /// Workflow tanımının geçerli olup olmadığını belirtir.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Doğrulama sırasında bulunan hata mesajlarını döner.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Başarılı doğrulama sonucu oluşturur.
    /// </summary>
    public static WorkflowValidationResult Success()
    {
        return new WorkflowValidationResult(true, []);
    }

    /// <summary>
    /// Hatalı doğrulama sonucu oluşturur.
    /// </summary>
    public static WorkflowValidationResult Failure(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new WorkflowValidationResult(false, errors);
    }
}