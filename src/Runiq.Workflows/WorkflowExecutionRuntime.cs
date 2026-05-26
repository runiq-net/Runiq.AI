namespace Runiq.Workflows;

/// <summary>
/// Workflow tanımlarını doğrulayıp çalıştıran varsayılan runtime'dır.
/// </summary>
public sealed class WorkflowExecutionRuntime : IWorkflowExecutionRuntime
{
    private readonly IWorkflowAgentResolver agentResolver;
    private readonly IWorkflowAgentExecutor agentExecutor;

    public WorkflowExecutionRuntime(
        IWorkflowAgentResolver agentResolver,
        IWorkflowAgentExecutor agentExecutor)
    {
        this.agentResolver = agentResolver ?? throw new ArgumentNullException(nameof(agentResolver));
        this.agentExecutor = agentExecutor ?? throw new ArgumentNullException(nameof(agentExecutor));
    }

    /// <inheritdoc />
    public async Task<WorkflowExecutionResult> ExecuteAsync(
        Workflow workflow,
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var validationResult = WorkflowValidator.Validate(workflow);

        if (!validationResult.IsValid)
        {
            return new WorkflowExecutionResult(
                    status: WorkflowExecutionStatus.Failed,
                    stepResults: [],
                    errorMessage: string.Join(Environment.NewLine, validationResult.Errors));
        }

        var currentStep = workflow.Steps[0];

        var stepResults = new List<WorkflowStepExecutionResult>();

        while (currentStep is not null)
        {
            var step = currentStep;
            var agent = agentResolver.Resolve(step.ExecutableType);

            try
            {
                var output = await agentExecutor.ExecuteAsync(
                    agent,
                    input,
                    cancellationToken);

                stepResults.Add(
                    new WorkflowStepExecutionResult(
                        stepId: step.Id,
                        agentType: agent.GetType(),
                        status: WorkflowStepExecutionStatus.Completed,
                        output: output));

                input = output;

                if (string.IsNullOrWhiteSpace(step.SuccessStepId))
                {
                    break;
                }

                currentStep = workflow.Steps.FirstOrDefault(
                    x => string.Equals(
                        x.Id,
                        step.SuccessStepId,
                        StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                stepResults.Add(
                    new WorkflowStepExecutionResult(
                        stepId: step.Id,
                        agentType: agent.GetType(),
                        status: WorkflowStepExecutionStatus.Failed,
                        errorMessage: ex.Message));

                if (step.FailureBehavior is WorkflowFailureBehavior.Continue)
                {
                    currentStep = workflow.Steps.FirstOrDefault(
                        x => string.Equals(
                            x.Id,
                            step.FailureStepId,
                            StringComparison.OrdinalIgnoreCase));

                    continue;
                }

                if (step.FailureBehavior is WorkflowFailureBehavior.GoTo)
                {
                    currentStep = workflow.Steps.FirstOrDefault(
                        x => string.Equals(
                            x.Id,
                            step.FailureStepId,
                            StringComparison.OrdinalIgnoreCase));

                    continue;
                }

                return new WorkflowExecutionResult(
                    status: WorkflowExecutionStatus.Failed,
                    stepResults: stepResults,
                    errorMessage: ex.Message);
            }
        }

        return new WorkflowExecutionResult(
             status: WorkflowExecutionStatus.Completed,
             stepResults: stepResults,
             finalOutput: input);

    }
}
