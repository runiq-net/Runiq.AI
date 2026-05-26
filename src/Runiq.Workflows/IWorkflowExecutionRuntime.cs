namespace Runiq.Workflows;

/// <summary>
/// Workflow tanımlarını çalıştıran runtime sözleşmesini temsil eder.
/// </summary>
public interface IWorkflowExecutionRuntime
{
    /// <summary>
    /// Verilen workflow tanımını kullanıcı girdisiyle çalıştırır.
    /// </summary>
    Task<WorkflowExecutionResult> ExecuteAsync(
        Workflow workflow,
        string input,
        CancellationToken cancellationToken = default);
}