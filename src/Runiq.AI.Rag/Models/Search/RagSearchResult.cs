using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;

namespace Runiq.AI.Rag.Models.Search;

/// <summary>
/// Represents a retrieved chunk match.
/// </summary>
public sealed record RagSearchResult
{
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagSearchResult"/> class.
    /// </summary>
    public RagSearchResult()
    {
    }

    /// <summary>
    /// Gets or initializes the matched chunk.
    /// </summary>
    public required RagChunk Chunk { get; init; }

    /// <summary>
    /// Gets or initializes the raw score returned by the vector store provider.
    /// The meaning and direction of this value are described by <see cref="Metric"/> and
    /// <see cref="HigherIsBetter"/>; it is not a provider-independent confidence value.
    /// </summary>
    public double RawScore { get; init; }

    /// <summary>
    /// Gets or initializes the provider-independent relevance in the inclusive range from zero to one, or
    /// <see langword="null"/> when the provider score cannot be normalized reliably.
    /// </summary>
    public double? Relevance { get; init; }

    /// <summary>
    /// Gets or initializes the metric identifier that defines the raw score semantics. Framework-defined
    /// identifiers are available from <see cref="RagScoreMetrics"/>. A missing metric is invalid.
    /// </summary>
    public string? Metric { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether larger raw scores represent better matches.
    /// </summary>
    public bool HigherIsBetter { get; init; }

    /// <summary>
    /// Gets or initializes search result metadata.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}

