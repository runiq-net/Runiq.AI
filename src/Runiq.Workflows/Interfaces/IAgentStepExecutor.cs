using Runiq.Workflows.Models;
using Runiq.Agents;

namespace Runiq.Workflows.Interfaces;

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
