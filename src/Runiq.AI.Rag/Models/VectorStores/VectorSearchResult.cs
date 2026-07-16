using Runiq.AI.Rag.Models.Metadata;

namespace Runiq.AI.Rag.Models.VectorStores;

/// <summary>
/// Represents a single vector record returned by a similarity query, carrying the data the retrieval pipeline needs
/// to build context: the record id, its stored content, its metadata, and explicit raw-score semantics.
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
    /// Gets or initializes the raw provider score. This value is not a provider-independent confidence value.
    /// </summary>
    public double RawScore { get; init; }

    /// <summary>
    /// Gets or initializes the provider-independent relevance in the inclusive range from zero to one, or
    /// <see langword="null"/> when no reliable normalization is available.
    /// </summary>
    public double? Relevance { get; init; }

    /// <summary>
    /// Gets or initializes the metric identifier that defines the raw score semantics.
    /// </summary>
    public string? Metric { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether larger raw scores represent better matches.
    /// </summary>
    public bool HigherIsBetter { get; init; }

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

