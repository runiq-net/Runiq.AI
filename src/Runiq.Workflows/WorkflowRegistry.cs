namespace Runiq.Workflows;

/// <summary>
/// Uygulama içinde kayıtlı workflow tanımlarını tutar.
/// </summary>
public sealed class WorkflowRegistry
{
    private readonly List<Workflow> workflows = [];

    /// <summary>
    /// Kayıtlı workflow tanımlarını döner.
    /// </summary>
    public IReadOnlyList<Workflow> Workflows => workflows;

    /// <summary>
    /// Yeni bir workflow tanımını registry'ye ekler.
    /// </summary>
    public WorkflowRegistry AddWorkflow(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        if (workflows.Any(existing =>
                string.Equals(existing.Id, workflow.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Workflow with id '{workflow.Id}' is already registered.");
        }

        workflows.Add(workflow);

        return this;
    }

    /// <summary>
    /// Verilen id'ye sahip workflow tanımını bulur.
    /// </summary>
    public Workflow? FindById(string workflowId)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            return null;
        }

        return workflows.FirstOrDefault(workflow =>
            string.Equals(workflow.Id, workflowId, StringComparison.OrdinalIgnoreCase));
    }
}