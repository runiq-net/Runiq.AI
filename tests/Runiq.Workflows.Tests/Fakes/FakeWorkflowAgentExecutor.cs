using Runiq.Agents;

namespace Runiq.Workflows.Tests.Fakes;

internal sealed class FakeWorkflowAgentExecutor : IWorkflowAgentExecutor
{
    private readonly Dictionary<string, string> outputsByAgentId = [];
    private readonly HashSet<string> failingAgentIds = [];
    private readonly Dictionary<string, IReadOnlyList<WorkflowToolCallExecutionResult>> toolCallsByAgentId = [];

    public FakeWorkflowAgentExecutor WithOutput(string agentId, string output)
    {
        outputsByAgentId[agentId] = output;
        return this;
    }

    public FakeWorkflowAgentExecutor WithFailure(string agentId)
    {
        failingAgentIds.Add(agentId);
        return this;
    }

    public FakeWorkflowAgentExecutor WithToolCalls(
        string agentId,
        IReadOnlyList<WorkflowToolCallExecutionResult> toolCalls)
    {
        toolCallsByAgentId[agentId] = toolCalls;
        return this;
    }

    public Task<WorkflowAgentExecutionResult> ExecuteAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        var toolCalls = toolCallsByAgentId.GetValueOrDefault(agent.Id, []);

        if (failingAgentIds.Contains(agent.Id))
        {
            return Task.FromResult(WorkflowAgentExecutionResult.Failure(
                $"Fake failure for agent '{agent.Id}'.",
                toolCalls));
        }

        if (outputsByAgentId.TryGetValue(agent.Id, out var output))
        {
            return Task.FromResult(WorkflowAgentExecutionResult.Success(
                output,
                toolCalls));
        }

        return Task.FromResult(WorkflowAgentExecutionResult.Success(
            input,
            toolCalls));
    }
}
