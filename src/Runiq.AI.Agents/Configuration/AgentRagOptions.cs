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
    /// Gets or sets how accepted retrieval context constrains the response. The default is
    /// <see cref="RagExecutionMode.Open"/> to preserve normal agent behavior when no context is accepted.
    /// </summary>
    public RagExecutionMode Mode { get; set; } = RagExecutionMode.Open;

    /// <summary>
    /// Gets or sets how a successful retrieval with no accepted context affects execution. The default is
    /// <see cref="RagNoContextBehavior.AnswerNormally"/>.
    /// </summary>
    public RagNoContextBehavior NoContextBehavior { get; set; } = RagNoContextBehavior.AnswerNormally;

    /// <summary>
    /// Gets or sets the optional minimum score a retrieved candidate must meet to become accepted context.
    /// A null value accepts every retrieved candidate. Score semantics remain vector-store specific.
    /// </summary>
    public double? MinimumRelevanceScore { get; set; }
}

