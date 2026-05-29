namespace Runiq.Workflows.Domain;

/// <summary>
/// Defines an executable flow made of ordered agent steps.
/// </summary>
public sealed class Flow
{
    private readonly List<FlowStep> steps = [];

    public Flow(string id, string name, string? instructions = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Flow id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Flow name cannot be empty.", nameof(name));
        }

        Id = id.Trim();
        Name = name.Trim();
        Instructions = instructions?.Trim();
    }

    public string Id { get; }

    public string Name { get; }

    public string? Instructions { get; }

    public IReadOnlyList<FlowStep> Steps => steps;

    public FlowStepBuilder Step<TAgent>(string stepId)
    {
        var step = new FlowStep(
            id: stepId,
            executableType: typeof(TAgent));

        steps.Add(step);

        return new FlowStepBuilder(this, step);
    }
}
