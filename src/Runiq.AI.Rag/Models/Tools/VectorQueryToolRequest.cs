using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.Models.Tools;

/// <summary>
/// Carries the provider-independent inputs an agent supplies when invoking the Vector Query Tool. The request
/// is a plain carrier contract in the same spirit as <see cref="RetrievalRequest"/>: it stores whatever values
/// the caller supplies — including a missing vector store name, a missing index name, an empty query, or a
/// non-positive result limit — without throwing, and stays free of embedding provider and vector store details.
/// Semantic validation is owned by the tool implementation and the existing retrieval pipeline it delegates to,
/// which represent an invalid request deterministically as a failed <see cref="VectorQueryToolResult"/> rather
/// than surfacing exceptions. The only contract-level nullability enforced here is a non-null
/// <see cref="MetadataFilter"/>, matching <see cref="RetrievalRequest"/>.
/// </summary>
public sealed class VectorQueryToolRequest
{
    private RetrievalMetadataFilter metadataFilter = RetrievalMetadataFilter.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorQueryToolRequest"/> class.
    /// </summary>
    public VectorQueryToolRequest()
    {
    }

    /// <summary>
    /// Gets or initializes the name that associates the tool invocation with a configured vector store. The
    /// request stores the value as supplied; a null, empty, or whitespace name is not rejected here — the tool
    /// implementation reports it deterministically as a failed result. This is an association value only and
    /// does not resolve a concrete provider at the contract level.
    /// </summary>
    public required string VectorStoreName { get; init; }

    /// <summary>
    /// Gets or initializes the target vector index the tool should query. The request stores the value as
    /// supplied; a null, empty, or whitespace index name is not rejected here — the tool implementation and the
    /// retrieval pipeline report it deterministically as a failed result.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// Gets or initializes the natural-language query text the agent wants to retrieve against. The request
    /// stores the value as supplied; an empty or whitespace query is not rejected here — the tool implementation
    /// and the retrieval pipeline report a semantically empty query deterministically as a failed result.
    /// </summary>
    public required string QueryText { get; init; }

    /// <summary>
    /// Gets or initializes the identifier of the embedding model the tool should use when turning
    /// <see cref="QueryText"/> into a query vector. This value is optional: a null value lets the tool
    /// implementation fall back to the embedding model configured for the retrieval pipeline. The identifier is
    /// a provider-independent selection value and does not resolve a concrete embedding provider at the contract
    /// level.
    /// </summary>
    public string? EmbeddingModel { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of matches the tool should return, ordered best match first.
    /// Defaults to five, matching the repository-standard retrieval default. The request stores the value as
    /// supplied; a zero or negative value is not rejected here — the retrieval pipeline reports it as a failed
    /// result.
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Gets or initializes the provider-independent metadata filter forwarded to the retrieval pipeline to
    /// narrow candidate matches. A null value is rejected so the tool can always rely on a non-null filter; use
    /// <see cref="RetrievalMetadataFilter.Empty"/> to express "no filtering".
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    public RetrievalMetadataFilter MetadataFilter
    {
        get => metadataFilter;
        init => metadataFilter = value ?? throw new ArgumentNullException(nameof(value));
    }
}

