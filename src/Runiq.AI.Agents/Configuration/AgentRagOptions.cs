namespace Runiq.AI.Agents.Configuration;

/// <summary>
/// Provides agent-level RAG retrieval configuration.
/// </summary>
public sealed class AgentRagOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether agent RAG retrieval is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the vector index name used by agent RAG queries.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// Gets or sets how retrieval failures affect model invocation.
    /// </summary>
    public RagExecutionMode Mode { get; set; } = RagExecutionMode.Required;
}

