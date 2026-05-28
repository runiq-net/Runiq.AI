using Runiq.Agents;

namespace Runiq.Workflows.Interfaces;

/// <summary>
/// Resolves step executable types to registered agent instances.
/// </summary>
public interface IAgentStepResolver
{
    Agent Resolve(Type agentType);
}
