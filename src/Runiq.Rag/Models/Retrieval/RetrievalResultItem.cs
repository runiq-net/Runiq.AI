using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.Retrieval;

/// <summary>
/// Represents a single match produced by a query-time retrieval. Each item exposes the retrieved chunk content
/// together with its provider-independent metadata and a similarity score, giving downstream steps (context
/// assembly, reranking) everything they need without binding to a vector store provider.
/// </summary>
public sealed class RetrievalResultItem
{
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalResultItem"/> class.
    /// </summary>
    public RetrievalResultItem()
    {
    }

    /// <summary>
    /// Gets or initializes the provider-independent identifier of the retrieved record, typically the chunk id
    /// the record was stored under. It lets callers correlate a match with the stored chunk without depending
    /// on any vector store provider.
    /// </summary>
    public string RecordId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the retrieved chunk content that this match represents.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the provider-independent metadata associated with the retrieved chunk, such as the
    /// source document or section it originated from. A null value is rejected so consumers can always rely on
    /// a non-null metadata instance.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes the provider-independent similarity score for this match, where higher values
    /// represent better matches. Providers that natively use distance metrics are expected to convert their
    /// distances into a higher-is-better score before populating this value.
    /// </summary>
    public double Score { get; init; }
}
