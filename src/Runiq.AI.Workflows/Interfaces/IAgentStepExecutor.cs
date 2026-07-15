using Runiq.AI.Workflows.Models;
using Runiq.AI.Agents;

namespace Runiq.AI.Workflows.Interfaces;

/// <summary>
/// Runs the agent bound to a flow step.
/// </summary>
public interface IAgentStepExecutor
{
    Task<AgentStepResult> ExecuteAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default);
}

