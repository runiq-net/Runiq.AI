namespace Runiq.Workflows.Domain;

/// <summary>
/// Defines an executable flow made of ordered agent steps.
/// </summary>
public sealed class Flow
{
    private readonly List<FlowStep> steps = [];

    /// <summary>
    /// Creates a new flow definition with the specified identity and optional instructions.
    /// </summary>
    /// <param name="id">Unique identifier for the flow.</param>
    /// <param name="name">Human-readable display name shown in the dashboard.</param>
    /// <param name="instructions">Optional global instructions applied to every step in the flow.</param>
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

    /// <summary>
    /// Gets the unique identifier of the flow.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the human-readable display name of the flow.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the optional global instructions applied to every step in the flow.
    /// </summary>
    public string? Instructions { get; }

    /// <summary>
    /// Gets the ordered list of steps that make up this flow.
    /// </summary>
    public IReadOnlyList<FlowStep> Steps => steps;

    /// <summary>
    /// Adds a new step to the flow that will be executed by the specified agent type.
    /// </summary>
    /// <typeparam name="TAgent">The agent type that will execute this step.</typeparam>
    /// <param name="stepId">Unique identifier for this step within the flow.</param>
    /// <returns>A <see cref="FlowStepBuilder"/> for configuring step transitions.</returns>
    public FlowStepBuilder Step<TAgent>(string stepId)
    {
        var step = new FlowStep(
            id: stepId,
            executableType: typeof(TAgent));

        steps.Add(step);

        return new FlowStepBuilder(this, step);
    }
}
