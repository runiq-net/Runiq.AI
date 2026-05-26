using Runiq.Agents;

namespace Runiq.Workflows;

/// <summary>
/// Tanımlı adımlardan oluşan çalıştırılabilir workflow modelini temsil eder.
/// </summary>
public sealed class Workflow
{
    private readonly List<WorkflowStep> steps = [];

    public Workflow(string id, string name, string? instructions = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Workflow id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Workflow name cannot be empty.", nameof(name));
        }

        Id = id.Trim();
        Name = name.Trim();
        Instructions = instructions?.Trim();
    }

    /// <summary>
    /// Workflow kimliğini döner.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Workflow görünen adını döner.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Workflow için açıklayıcı yönergeleri döner.
    /// </summary>
    public string? Instructions { get; }

    /// <summary>
    /// Workflow içindeki tanımlı adımları döner.
    /// </summary>
    public IReadOnlyList<WorkflowStep> Steps => steps;

    /// <summary>
    /// Workflow'a yeni bir agent adımı ekler.
    /// </summary>
    public WorkflowStepBuilder Step<TAgent>(string stepId)
        where TAgent : Agent
    {
        var step = new WorkflowStep(
            id: stepId,
            executableType: typeof(TAgent));

        steps.Add(step);

        return new WorkflowStepBuilder(this, step);
    }

}