using Runiq.AI.Rag.Models.Metadata;

namespace Runiq.AI.Rag.Models.VectorStores;

/// <summary>
/// Represents a single vector record returned by a similarity query, carrying the data the retrieval pipeline needs
/// to build context: the record id, its stored content, its metadata, and a provider-independent similarity score.
/// </summary>
public sealed class VectorSearchResult
{
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorSearchResult"/> class.
    /// </summary>
    public VectorSearchResult()
    {
    }

    /// <summary>
    /// Gets or initializes the vector identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or initializes the provider-independent relevance or similarity score.
    /// Higher values represent better matches. Providers that use distance metrics should convert distances
    /// to a higher-is-better score instead of returning raw distance values here.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Gets or initializes the content associated with the vector.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes record metadata.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes the vector values when requested by the query.
    /// </summary>
    public IReadOnlyList<float>? Values { get; init; }
}

