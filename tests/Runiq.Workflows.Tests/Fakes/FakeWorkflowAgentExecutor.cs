using Runiq.Agents;

namespace Runiq.Workflows.Tests.Fakes;

internal sealed class FakeWorkflowAgentExecutor : IWorkflowAgentExecutor
{
    private readonly Dictionary<string, string> outputsByAgentId = [];
    private readonly HashSet<string> failingAgentIds = [];

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

    public Task<string> ExecuteAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        if (failingAgentIds.Contains(agent.Id))
        {
            throw new InvalidOperationException(
                $"Fake failure for agent '{agent.Id}'.");
        }

        if (outputsByAgentId.TryGetValue(agent.Id, out var output))
        {
            return Task.FromResult(output);
        }

        return Task.FromResult(input);
    }
}