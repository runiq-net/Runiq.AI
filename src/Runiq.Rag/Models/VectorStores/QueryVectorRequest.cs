using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Represents a request to query vector records by similarity.
/// </summary>
public sealed class QueryVectorRequest
{
    private RagMetadata metadata = RagMetadata.Empty;
    private RagMetadata metadataFilter = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryVectorRequest"/> class.
    /// </summary>
    public QueryVectorRequest()
    {
    }

    /// <summary>
    /// Gets or initializes the vector index name to query.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// Gets or initializes the query vector values.
    /// </summary>
    public required IReadOnlyList<float> Values { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of matches to return. Query results are returned best match first.
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Gets or initializes exact-match metadata filters for the query.
    /// </summary>
    public RagMetadata MetadataFilter
    {
        get => metadataFilter;
        init => metadataFilter = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes a value indicating whether record metadata should be included in results.
    /// </summary>
    public bool IncludeMetadata { get; init; } = true;

    /// <summary>
    /// Gets or initializes a value indicating whether vector values should be included in results.
    /// </summary>
    public bool IncludeVectors { get; init; }

    /// <summary>
    /// Gets or initializes request metadata.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
