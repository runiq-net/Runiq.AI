namespace Runiq.Workflows;

/// <summary>
/// Runiq workflow kayıtları için yapılandırma seçeneklerini temsil eder.
/// </summary>
public sealed class WorkflowOptions
{
    private readonly WorkflowRegistry registry = new();

    /// <summary>
    /// Yapılandırma sırasında eklenen workflow tanımlarını döner.
    /// </summary>
    public IReadOnlyList<Workflow> Workflows => registry.Workflows;

    /// <summary>
    /// Yeni bir workflow tanımını doğrulayarak kayıt listesine ekler.
    /// </summary>
    public WorkflowOptions AddWorkflow(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var validationResult = WorkflowValidator.Validate(workflow);

        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                $"Workflow '{workflow.Id}' is invalid:{Environment.NewLine}" +
                string.Join(Environment.NewLine, validationResult.Errors));
        }

        registry.AddWorkflow(workflow);

        return this;
    }
}
