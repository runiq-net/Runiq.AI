namespace Runiq.Workflows.Domain;

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

    public FlowStepBuilder Step<TAgent>(string stepId)
    {
        return flow.Step<TAgent>(stepId);
    }

    public Flow Build()
    {
        return flow;
    }

    public FlowStepBuilder OnSuccess(string nextStepId)
    {
        if (string.IsNullOrWhiteSpace(nextStepId))
        {
            throw new ArgumentException("Success step id cannot be empty.", nameof(nextStepId));
        }

        step.SuccessStepId = nextStepId.Trim();

        return this;
    }

    public FlowStepBuilder OnSuccessEnd()
    {
        step.SuccessStepId = null;

        return this;
    }

    public FlowStepBuilder OnFailureStop()
    {
        step.FailureBehavior = FailureBehavior.Stop;
        step.FailureStepId = null;

        return this;
    }

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
