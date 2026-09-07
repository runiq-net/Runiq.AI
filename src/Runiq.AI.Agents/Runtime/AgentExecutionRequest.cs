namespace Runiq.AI.Agents.Runtime;

/// <summary>Pairs a configured agent definition with its complete per-call query.</summary>
public sealed class AgentExecutionRequest
{
    /// <summary>Initializes a provider-neutral execution request without creating run state.</summary>
    /// <param name="agent">The configured agent definition, shared read-only during execution.</param>
    /// <param name="query">The query including its input and all per-call options.</param>
    public AgentExecutionRequest(Agent agent, AgentQuery query)
    {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        Query = query ?? throw new ArgumentNullException(nameof(query));
    }

    /// <summary>Gets the configured agent definition.</summary>
    public Agent Agent { get; }

    /// <summary>Gets the original query without dropping or copying its options.</summary>
    public AgentQuery Query { get; }
}
