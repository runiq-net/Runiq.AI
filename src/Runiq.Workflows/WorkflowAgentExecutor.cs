using Runiq.Agents;
using Runiq.Agents.Runtime;

namespace Runiq.Workflows;

/// <summary>
/// Workflow adımlarındaki agent'ları Runiq agent execution runtime üzerinden çalıştırır.
/// </summary>
public sealed class WorkflowAgentExecutor : IWorkflowAgentExecutor
{
    private readonly AgentExecutionRuntime agentExecutionRuntime;

    public WorkflowAgentExecutor(AgentExecutionRuntime agentExecutionRuntime)
    {
        this.agentExecutionRuntime = agentExecutionRuntime
            ?? throw new ArgumentNullException(nameof(agentExecutionRuntime));
    }

    /// <inheritdoc />
    public async Task<WorkflowAgentExecutionResult> ExecuteAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        var result = await agentExecutionRuntime.ExecuteAsync(
            agent,
            input,
            cancellationToken);

        var toolCalls = result.Steps
            .Where(step => step.Kind == AgentExecutionStepKind.ToolCall)
            .Select(MapToolCall)
            .ToArray();

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Message))
        {
            return WorkflowAgentExecutionResult.Success(result.Message, toolCalls);
        }

        return WorkflowAgentExecutionResult.Failure(
            result.ErrorMessage ?? "Agent execution failed.",
            toolCalls);
    }

    private static WorkflowToolCallExecutionResult MapToolCall(AgentExecutionStep step)
    {
        return new WorkflowToolCallExecutionResult(
            toolCallId: step.ToolCallId,
            toolName: step.ToolName,
            status: MapToolCallStatus(step.Status),
            argumentsJson: step.ArgumentsJson,
            outputJson: step.OutputJson,
            errorCode: step.ErrorCode,
            errorMessage: step.ErrorMessage,
            startedAt: step.StartedAt,
            completedAt: step.CompletedAt);
    }

    private static WorkflowToolCallExecutionStatus MapToolCallStatus(
        AgentExecutionStepStatus status)
    {
        return status switch
        {
            AgentExecutionStepStatus.Completed => WorkflowToolCallExecutionStatus.Completed,
            AgentExecutionStepStatus.Failed => WorkflowToolCallExecutionStatus.Failed,
            _ => WorkflowToolCallExecutionStatus.Running
        };
    }
}
