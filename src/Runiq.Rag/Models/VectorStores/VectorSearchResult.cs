using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Represents a vector record returned by a similarity query.
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
    /// Gets or initializes the relevance or similarity score.
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
