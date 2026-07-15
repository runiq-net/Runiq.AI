namespace Runiq.AI.Workflows.Domain;

/// <summary>
/// Fluent builder for defining flow step transitions.
/// </summary>
public sealed class FlowStepBuilder
{
    private readonly Flow flow;
    private readonly FlowStep step;

    internal FlowStepBuilder(Flow flow, FlowStep step)
    {
        this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
        this.step = step ?? throw new ArgumentNullException(nameof(step));
    }

    /// <summary>
    /// Adds a new step to the flow and returns its builder for further configuration.
    /// </summary>
    /// <typeparam name="TAgent">The agent type that will execute the new step.</typeparam>
    /// <param name="stepId">Unique identifier for the new step.</param>
    /// <returns>A <see cref="FlowStepBuilder"/> for configuring the new step's transitions.</returns>
    public FlowStepBuilder Step<TAgent>(string stepId)
    {
        return flow.Step<TAgent>(stepId);
    }

    /// <summary>
    /// Finalizes the flow definition and returns the completed <see cref="Flow"/> instance.
    /// </summary>
    /// <returns>The fully configured <see cref="Flow"/>.</returns>
    public Flow Build()
    {
        return flow;
    }

    /// <summary>
    /// Configures the step to transition to the specified step on success.
    /// </summary>
    /// <param name="nextStepId">Identifier of the step to execute after successful completion.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public FlowStepBuilder OnSuccess(string nextStepId)
    {
        if (string.IsNullOrWhiteSpace(nextStepId))
        {
            throw new ArgumentException("Success step id cannot be empty.", nameof(nextStepId));
        }

        step.SuccessStepId = nextStepId.Trim();

        return this;
    }

    /// <summary>
    /// Configures the step to end the flow on success (no subsequent step).
    /// </summary>
    /// <returns>This builder instance for fluent chaining.</returns>
    public FlowStepBuilder OnSuccessEnd()
    {
        step.SuccessStepId = null;

        return this;
    }

    /// <summary>
    /// Configures the step to stop the entire flow on failure.
    /// </summary>
    /// <returns>This builder instance for fluent chaining.</returns>
    public FlowStepBuilder OnFailureStop()
    {
        step.FailureBehavior = FailureBehavior.Stop;
        step.FailureStepId = null;

        return this;
    }

    /// <summary>
    /// Configures the step to continue to the specified step on failure,
    /// allowing the flow to proceed despite the error.
    /// </summary>
    /// <param name="nextStepId">Identifier of the step to execute after failure.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public FlowStepBuilder OnFailureContinue(string nextStepId)
    {
        if (string.IsNullOrWhiteSpace(nextStepId))
        {
            throw new ArgumentException("Failure continuation step id cannot be empty.", nameof(nextStepId));
        }

        step.FailureBehavior = FailureBehavior.Continue;
        step.FailureStepId = nextStepId.Trim();

        return this;
    }

    /// <summary>
    /// Configures the step to jump to a specific fallback step on failure.
    /// </summary>
    /// <param name="fallbackStepId">Identifier of the fallback step to execute on failure.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public FlowStepBuilder OnFailureGoTo(string fallbackStepId)
    {
        if (string.IsNullOrWhiteSpace(fallbackStepId))
        {
            throw new ArgumentException("Fallback step id cannot be empty.", nameof(fallbackStepId));
        }

        step.FailureBehavior = FailureBehavior.GoTo;
        step.FailureStepId = fallbackStepId.Trim();

        return this;
    }
}

