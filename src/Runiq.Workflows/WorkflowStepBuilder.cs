using Runiq.Agents;

namespace Runiq.Workflows;

/// <summary>
/// Workflow adımı için başarı ve hata geçişlerini tanımlayan fluent builder'dır.
/// </summary>
public sealed class WorkflowStepBuilder
{
    private readonly Workflow workflow;
    private readonly WorkflowStep step;

    internal WorkflowStepBuilder(Workflow workflow, WorkflowStep step)
    {
        this.workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        this.step = step ?? throw new ArgumentNullException(nameof(step));
    }

    /// <summary>
    /// <summary>
    /// Workflow'a yeni bir agent adımı ekler.
    /// </summary>
    public WorkflowStepBuilder Step<TAgent>(string stepId)
        where TAgent : Agent
    {
        return workflow.Step<TAgent>(stepId);
    }

    /// <summary>
    /// Tanımlanan workflow modelini döner.
    /// </summary>
    public Workflow Build()
    {
        return workflow;
    }

    /// <summary>
    /// Adım başarılı olduğunda gidilecek sonraki adımı tanımlar.
    /// </summary>
    public WorkflowStepBuilder OnSuccess(string nextStepId)
    {
        if (string.IsNullOrWhiteSpace(nextStepId))
        {
            throw new ArgumentException("Success step id cannot be empty.", nameof(nextStepId));
        }

        step.SuccessStepId = nextStepId.Trim();

        return this;
    }

    /// <summary>
    /// Adım başarılı olduğunda workflow çalışmasını sonlandırır.
    /// </summary>
    public WorkflowStepBuilder OnSuccessEnd()
    {
        step.SuccessStepId = null;
   

        return this;
    }

    /// <summary>
    /// Adım hata verdiğinde workflow çalışmasını başarısız olarak sonlandırır.
    /// </summary>
    public WorkflowStepBuilder OnFailureStop()
    {
        step.FailureBehavior = WorkflowFailureBehavior.Stop;
        step.FailureStepId = null;

        return this;
    }

    /// <summary>
    /// Adım hata verdiğinde belirtilen adıma devam eder.
    /// </summary>
    public WorkflowStepBuilder OnFailureContinue(string nextStepId)
    {
        if (string.IsNullOrWhiteSpace(nextStepId))
        {
            throw new ArgumentException("Failure continuation step id cannot be empty.", nameof(nextStepId));
        }

        step.FailureBehavior = WorkflowFailureBehavior.Continue;
        step.FailureStepId = nextStepId.Trim();

        return this;
    }

    /// <summary>
    /// Adım hata verdiğinde belirtilen fallback adıma yönlenir.
    /// </summary>
    public WorkflowStepBuilder OnFailureGoTo(string fallbackStepId)
    {
        if (string.IsNullOrWhiteSpace(fallbackStepId))
            throw new ArgumentException("Fallback step id cannot be empty.", nameof(fallbackStepId));
   
        step.FailureBehavior = WorkflowFailureBehavior.GoTo;
        step.FailureStepId = fallbackStepId.Trim();

        return this;
    }
}