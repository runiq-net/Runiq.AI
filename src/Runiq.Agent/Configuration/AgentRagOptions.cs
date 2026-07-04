namespace Runiq.Agents.Configuration;

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
    /// Gets or sets the vector store name associated with the agent's Vector Query Tool. It is carried as an
    /// association/configuration value only: the agent layer does not use it for provider routing or vector
    /// store selection.
    /// </summary>
    public string? VectorStoreName { get; set; }

    /// <summary>
    /// Gets or sets the optional embedding model identifier associated with the agent's Vector Query Tool. It
    /// is carried as an association/configuration value only and does not trigger provider resolution in the
    /// agent layer.
    /// </summary>
    public string? EmbeddingModel { get; set; }
}
