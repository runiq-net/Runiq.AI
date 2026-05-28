namespace Runiq.Workflows.Domain;

/// <summary>
/// Defines a single executable step inside a flow.
/// </summary>
public sealed class FlowStep
{
    internal FlowStep(string id, Type executableType)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Flow step id cannot be empty.", nameof(id));
        }

        Id = id.Trim();
        ExecutableType = executableType ?? throw new ArgumentNullException(nameof(executableType));
    }

    public string Id { get; }

    public Type ExecutableType { get; }

    public string? SuccessStepId { get; internal set; }

    public FailureBehavior FailureBehavior { get; internal set; } = FailureBehavior.Stop;

    public string? FailureStepId { get; internal set; }
}
