namespace Runiq.Agents.Runtime;

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

    /// <summary>
    /// Gets or initializes the vector index name override used for this agent RAG query.
    /// </summary>
    public string? IndexName { get; init; }
}
