namespace Runiq.Rag.Models.Retrieval;

/// <summary>
/// Carries the provider-independent inputs required to run a single query-time retrieval against a vector
/// index. The request is a plain carrier contract: it stores whatever values the caller supplies — including a
/// missing index name or a non-positive result limit — without throwing, and stays free of embedding provider
/// and vector store details. Semantic validation is owned by the retrieval pipeline, which represents an
/// invalid request deterministically as a failed retrieval result with
/// <see cref="RetrievalErrorCode.InvalidRequest"/> instead of surfacing exceptions. The query can be expressed
/// as raw text (to be embedded later by the retrieval pipeline), as a pre-computed query vector, or both;
/// <see cref="HasRetrievableQuery"/> identifies semantically empty query input.
/// </summary>
public sealed class RetrievalRequest
{
    private RetrievalMetadataFilter metadataFilter = RetrievalMetadataFilter.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalRequest"/> class.
    /// </summary>
    public RetrievalRequest()
    {
    }

    /// <summary>
    /// Gets or initializes the target vector index that the retrieval should query. The request stores the
    /// value as supplied; a null, empty, or whitespace index name is not rejected here — the retrieval
    /// pipeline reports it as an <see cref="RetrievalErrorCode.InvalidRequest"/> failure result.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// Gets or initializes the natural-language query text to retrieve against. This value is optional: a
    /// request may instead supply a pre-computed <see cref="QueryVector"/>. When present, the retrieval
    /// pipeline is expected to embed this text before querying the vector index.
    /// </summary>
    public string? QueryText { get; init; }

    /// <summary>
    /// Gets or initializes a pre-computed query vector to retrieve against. This value is optional: a request
    /// may instead supply <see cref="QueryText"/> and let the retrieval pipeline embed it. When present, the
    /// pipeline can query the vector index directly without invoking an embedding provider.
    /// </summary>
    public IReadOnlyList<float>? QueryVector { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request carries data that is meaningful for retrieval, namely
    /// non-whitespace query text or a non-empty query vector. The retrieval pipeline uses this to represent a
    /// semantically empty request deterministically as <see cref="RetrievalErrorCode.InvalidRequest"/> rather
    /// than issuing a query that cannot match anything.
    /// </summary>
    public bool HasRetrievableQuery =>
        !string.IsNullOrWhiteSpace(QueryText) || QueryVector is { Count: > 0 };

    /// <summary>
    /// Gets or initializes the maximum number of matches the retrieval should return, ordered best match
    /// first. Defaults to five. The request stores the value as supplied; a zero or negative value is not
    /// rejected here — the retrieval pipeline reports it as an
    /// <see cref="RetrievalErrorCode.InvalidRequest"/> failure result.
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Gets or initializes the provider-independent metadata filter applied to candidate matches. A null value
    /// is rejected so the retrieval pipeline can always rely on a non-null filter; use
    /// <see cref="RetrievalMetadataFilter.Empty"/> to express "no filtering".
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    public RetrievalMetadataFilter MetadataFilter
    {
        get => metadataFilter;
        init => metadataFilter = value ?? throw new ArgumentNullException(nameof(value));
    }
}
