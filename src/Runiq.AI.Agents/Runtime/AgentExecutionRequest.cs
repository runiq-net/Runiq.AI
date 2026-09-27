namespace Runiq.AI.Agents.Runtime;

/// <summary>Pairs a configured agent definition with its complete per-call query.</summary>
public sealed class AgentExecutionRequest
{
    /// <summary>Initializes a provider-neutral execution request without creating run state.</summary>
    /// <param name="agent">The configured agent definition, shared read-only during execution.</param>
    /// <param name="query">The query including its input and all per-call options.</param>
    /// <param name="memory">The host-authorized Memory context, or null for disabled execution.</param>
    public AgentExecutionRequest(Agent agent, AgentQuery query, Runiq.AI.Memory.Models.MemoryContext? memory = null)
    {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        Query = query ?? throw new ArgumentNullException(nameof(query));
        Memory = memory;
    }

    /// <summary>Gets the configured agent definition.</summary>
    public Agent Agent { get; }

    /// <summary>Gets the original query without dropping or copying its options.</summary>
    public AgentQuery Query { get; }

    /// <summary>Gets the immutable authorization result supplied by runtime preflight, never by a client DTO.</summary>
    public Runiq.AI.Memory.Models.MemoryContext? Memory { get; }
}
