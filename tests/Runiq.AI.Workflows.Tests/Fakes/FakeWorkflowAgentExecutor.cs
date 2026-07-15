using Runiq.AI.Workflows.Services;
using Runiq.AI.Workflows.Interfaces;
using Runiq.AI.Workflows.Infrastructure;
using Runiq.AI.Workflows.Domain;
using Runiq.AI.Workflows.Models;
using Runiq.AI.Agents;

namespace Runiq.AI.Workflows.Tests.Fakes;

internal sealed class FakeRuniqAgentStepExecutor : IAgentStepExecutor
{
    private readonly Dictionary<string, string> outputsByAgentId = [];
    private readonly HashSet<string> failingAgentIds = [];
    private readonly Dictionary<string, IReadOnlyList<ToolCallRunResult>> toolCallsByAgentId = [];

    public FakeRuniqAgentStepExecutor WithOutput(string agentId, string output)
    {
        outputsByAgentId[agentId] = output;
        return this;
    }

    public FakeRuniqAgentStepExecutor WithFailure(string agentId)
    {
        failingAgentIds.Add(agentId);
        return this;
    }

    public FakeRuniqAgentStepExecutor WithToolCalls(
        string agentId,
        IReadOnlyList<ToolCallRunResult> toolCalls)
    {
        toolCallsByAgentId[agentId] = toolCalls;
        return this;
    }

    public Task<AgentStepResult> ExecuteAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        var toolCalls = toolCallsByAgentId.GetValueOrDefault(agent.Id, []);

        if (failingAgentIds.Contains(agent.Id))
        {
            return Task.FromResult(AgentStepResult.Failure(
                $"Fake failure for agent '{agent.Id}'.",
                toolCalls));
        }

        if (outputsByAgentId.TryGetValue(agent.Id, out var output))
        {
            return Task.FromResult(AgentStepResult.Success(
                output,
                toolCalls));
        }

        return Task.FromResult(AgentStepResult.Success(
            input,
            toolCalls));
    }
}

