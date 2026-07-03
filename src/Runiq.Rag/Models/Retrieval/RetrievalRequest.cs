namespace Runiq.Rag.Models.Retrieval;

/// <summary>
/// Carries the provider-independent inputs required to run a single query-time retrieval against a vector
/// index. The request stays free of embedding provider and vector store details: it can express the query as
/// raw text (to be embedded later by the retrieval pipeline), as a pre-computed query vector, or both. It fails
/// fast on structurally invalid values such as a missing index name or invalid result limit, while
/// <see cref="HasRetrievableQuery"/> identifies semantically empty query input for deterministic result-based
/// handling by the retrieval layer.
/// </summary>
public sealed class RetrievalRequest
{
    private string indexName = null!;
    private int topK = 5;
    private RetrievalMetadataFilter metadataFilter = RetrievalMetadataFilter.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalRequest"/> class.
    /// </summary>
    public RetrievalRequest()
    {
    }

    /// <summary>
    /// Gets or initializes the target vector index that the retrieval should query.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the index name is null, empty, or contains only whitespace.
    /// </exception>
    public required string IndexName
    {
        get => indexName;
        init => indexName = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Retrieval request index name is required.", nameof(IndexName))
            : value;
    }

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
    /// non-whitespace query text or a non-empty query vector. Callers use this to represent a semantically
    /// empty request deterministically as <see cref="RetrievalErrorCode.InvalidRequest"/> rather than issuing
    /// a query that cannot match anything.
    /// </summary>
    public bool HasRetrievableQuery =>
        !string.IsNullOrWhiteSpace(QueryText) || QueryVector is { Count: > 0 };

    /// <summary>
    /// Gets or initializes the maximum number of matches the retrieval should return, ordered best match
    /// first. Defaults to five.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is zero or negative.
    /// </exception>
    public int TopK
    {
        get => topK;
        init => topK = value <= 0
            ? throw new ArgumentOutOfRangeException(nameof(TopK), value, "Retrieval request TopK must be greater than zero.")
            : value;
    }

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
