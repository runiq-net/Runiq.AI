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
            var stepInput = input;
            var agent = agentResolver.Resolve(step.ExecutableType);

            try
            {
                var agentResult = await agentExecutor.ExecuteAsync(
                    agent,
                    input,
                    cancellationToken);

                if (!agentResult.IsSuccess)
                {
                    stepResults.Add(
                        new WorkflowStepExecutionResult(
                            stepId: step.Id,
                            agentType: agent.GetType(),
                            status: WorkflowStepExecutionStatus.Failed,
                            input: stepInput,
                            errorMessage: agentResult.ErrorMessage,
                            toolCalls: agentResult.ToolCalls));

                    currentStep = ResolveFailureStep(workflow, step);

                    if (currentStep is not null)
                    {
                        continue;
                    }

                    return new WorkflowExecutionResult(
                        status: WorkflowExecutionStatus.Failed,
                        stepResults: stepResults,
                        errorMessage: agentResult.ErrorMessage);
                }

                var output = agentResult.Output ?? string.Empty;

                stepResults.Add(
                    new WorkflowStepExecutionResult(
                        stepId: step.Id,
                        agentType: agent.GetType(),
                        status: WorkflowStepExecutionStatus.Completed,
                        input: stepInput,
                        output: output,
                        toolCalls: agentResult.ToolCalls));

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
                        input: stepInput,
                        errorMessage: ex.Message));

                currentStep = ResolveFailureStep(workflow, step);

                if (currentStep is not null)
                {
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

    private static WorkflowStep? ResolveFailureStep(
        Workflow workflow,
        WorkflowStep step)
    {
        if (step.FailureBehavior is not WorkflowFailureBehavior.Continue &&
            step.FailureBehavior is not WorkflowFailureBehavior.GoTo)
        {
            return null;
        }

        return workflow.Steps.FirstOrDefault(
            x => string.Equals(
                x.Id,
                step.FailureStepId,
                StringComparison.OrdinalIgnoreCase));
    }
}
