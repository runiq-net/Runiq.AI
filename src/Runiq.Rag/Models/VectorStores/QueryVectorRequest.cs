using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Represents a provider-independent request to retrieve vector records by similarity to a query vector.
/// This is the query-time counterpart of the upsert contract: the retrieval pipeline uses it to ask a vector store
/// for the records that best match a query embedding within a single index.
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
    /// Gets or initializes the name of the vector index to search. The query is isolated to this index; records in
    /// other indexes are never considered.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// Gets or initializes the query vector values that stored records are scored against. The dimension must match
    /// the dimension of the target index.
    /// </summary>
    public required IReadOnlyList<float> Values { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of matches to return. Results are ordered best match first and are
    /// truncated to this count. Must be greater than zero.
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Gets or initializes provider-independent exact-match metadata filters for the query. Filters are carried as
    /// plain key/value pairs so they remain provider-agnostic; a record must match every entry to be returned, and an
    /// empty filter applies no restriction.
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
