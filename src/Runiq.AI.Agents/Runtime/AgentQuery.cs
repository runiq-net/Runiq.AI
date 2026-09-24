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
    /// <remarks>This is not a RunId. The trusted host must authorize access to the session before supplying it.</remarks>
    public string? ProviderSessionId { get; init; }

    /// <summary>
    /// Gets or initializes the vector index name override used for this agent RAG query.
    /// </summary>
    public string? IndexName { get; init; }
}

