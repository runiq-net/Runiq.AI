using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.Models.Queries;

/// <summary>
/// Represents a retrieval query.
/// </summary>
public sealed class RagQuery
{
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagQuery"/> class.
    /// </summary>
    public RagQuery()
    {
    }

    /// <summary>
    /// Gets or initializes the query text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets or initializes the vector index name to query.
    /// </summary>
    public string? IndexName { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of raw candidates to retrieve. This value does not guarantee
    /// relevance or acceptance as Agent Chat context.
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>Gets or initializes the retrieval mode. The default is semantic.</summary>
    public RagRetrievalMode Mode { get; init; } = RagRetrievalMode.Semantic;

    /// <summary>
    /// Gets or initializes query metadata.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}

