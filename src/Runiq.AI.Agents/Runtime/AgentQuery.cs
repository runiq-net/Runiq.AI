namespace Runiq.AI.Agents.Runtime;

/// <summary>
/// Represents a runtime agent query with optional per-call RAG overrides.
/// </summary>
public sealed class AgentQuery
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentQuery"/> class.
    /// </summary>
    /// <param name="message">The user message sent to the agent.</param>
    public AgentQuery(string message)
    {
        Message = message ?? string.Empty;
    }

    /// <summary>
    /// Gets the user message sent to the agent.
    /// </summary>
    public string Message { get; }

    /// <summary>Gets or initializes an explicit provider session to resume, when supported by the executor.</summary>
    /// <remarks>This is neither a RunId nor a Memory ThreadId. The trusted host must authorize access to the
    /// native session before supplying it. Enabled framework Memory rejects mixed provider-session input.</remarks>
    public string? ProviderSessionId { get; init; }

    /// <summary>Gets or initializes a caller-retained logical Memory turn identity; defaults to the fresh RunId.</summary>
    /// <remarks>Reuse with the returned ThreadId detects duplicate invocations and rejects them before model/tool execution.
    /// It does not resume execution or guarantee exactly-once external side effects.</remarks>
    public string? MemoryTurnId { get; init; }

    /// <summary>Gets or initializes an untrusted Memory resource/thread reference for host authorization.</summary>
    /// <remarks>Contains no verified caller identity. Null ThreadId requests a new thread; provider sessions are independent.</remarks>
    public Runiq.AI.Memory.Models.MemoryReference? Memory { get; init; }

    /// <summary>
    /// Gets or initializes the vector index name override used for this agent RAG query.
    /// </summary>
    public string? IndexName { get; init; }
}

