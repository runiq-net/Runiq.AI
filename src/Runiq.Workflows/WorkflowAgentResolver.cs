using Runiq.Agents;

namespace Runiq.Workflows;

/// <summary>
/// Kayıtlı agent listesinden workflow adımı için uygun agent instance'ını çözer.
/// </summary>
public sealed class WorkflowAgentResolver : IWorkflowAgentResolver
{
    private readonly IReadOnlyDictionary<Type, Agent> agentsByType;

    public WorkflowAgentResolver(IEnumerable<Agent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        agentsByType = agents.ToDictionary(
            agent => agent.GetType(),
            agent => agent);
    }

    /// <inheritdoc />
    public Agent Resolve(Type agentType)
    {
        ArgumentNullException.ThrowIfNull(agentType);

        if (agentsByType.TryGetValue(agentType, out var agent))
        {
            return agent;
        }

        throw new InvalidOperationException(
            $"No registered workflow agent found for type '{agentType.FullName}'.");
    }
}