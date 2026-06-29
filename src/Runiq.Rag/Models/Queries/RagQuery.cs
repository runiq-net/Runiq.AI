using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.Queries;

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
    /// Gets or initializes the maximum number of matches to retrieve.
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Gets or initializes query metadata.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
