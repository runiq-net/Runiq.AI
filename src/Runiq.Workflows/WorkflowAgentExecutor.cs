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
    public async Task<string> ExecuteAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        var result = await agentExecutionRuntime.ExecuteAsync(
            agent,
            input,
            cancellationToken);

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Message))
        {
            return result.Message;
        }

        throw new InvalidOperationException(
            result.ErrorMessage ?? "Agent execution failed.");
    }
}