using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.Models.VectorStores;

/// <summary>
/// Represents a provider-independent request to retrieve vector records by similarity to a query vector.
/// This is the query-time counterpart of the upsert contract: the retrieval pipeline uses it to ask a vector store
/// for the records that best match a query embedding within a single index.
/// </summary>
public sealed class QueryVectorRequest
{
    private RagMetadata metadata = RagMetadata.Empty;
    private RetrievalMetadataFilter metadataFilter = RetrievalMetadataFilter.Empty;

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
    /// Gets or initializes the maximum number of raw candidates to return. Results are ordered according to their
    /// explicit metric direction and truncated to this count. This is not an acceptance guarantee and must be
    /// greater than zero.
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Gets or initializes the provider-independent metadata filter applied to candidate records before
    /// similarity scoring. The filter is a list of criteria combined with logical AND semantics: a record must
    /// satisfy every criterion to be returned. An empty filter applies no restriction, and a null value is
    /// rejected so vector stores can always rely on a non-null filter; use
    /// <see cref="RetrievalMetadataFilter.Empty"/> to express "no filtering".
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    public RetrievalMetadataFilter MetadataFilter
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

