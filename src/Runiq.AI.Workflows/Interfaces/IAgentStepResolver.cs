using Runiq.AI.Agents;

namespace Runiq.AI.Workflows.Interfaces;

/// <summary>
/// Resolves step executable types to registered agent instances.
/// </summary>
public interface IAgentStepResolver
{
    Agent Resolve(Type agentType);
}

