namespace Runiq.AI.Workflows.Domain;

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

    /// <summary>
    /// Gets the unique identifier of this step within the flow.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the CLR type of the agent that will execute this step.
    /// </summary>
    public Type ExecutableType { get; }

    /// <summary>
    /// Gets or sets the identifier of the next step to execute on success.
    /// When <c>null</c>, the flow ends after this step completes successfully.
    /// </summary>
    public string? SuccessStepId { get; internal set; }

    /// <summary>
    /// Gets or sets the behavior to apply when this step fails.
    /// Defaults to <see cref="Domain.FailureBehavior.Stop"/>.
    /// </summary>
    public FailureBehavior FailureBehavior { get; internal set; } = FailureBehavior.Stop;

    /// <summary>
    /// Gets or sets the identifier of the step to execute on failure
    /// when <see cref="FailureBehavior"/> is <see cref="Domain.FailureBehavior.Continue"/> or <see cref="Domain.FailureBehavior.GoTo"/>.
    /// </summary>
    public string? FailureStepId { get; internal set; }
}

