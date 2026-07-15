using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.Abstractions.Retrieval;

/// <summary>
/// Orchestrates the provider-independent query-time retrieval pipeline: it embeds the request's query text
/// through the embedding abstraction, runs a similarity query against the target vector index through the
/// vector store abstraction, and returns the matches as a standard <see cref="RetrievalResult"/>. The pipeline
/// is an orchestration layer only — implementations must not know any concrete embedding provider or vector
/// store implementation, must not compute similarity scores of their own (scores come from the vector store
/// query result), and must not perform reranking, prompt building, or context assembly.
/// </summary>
/// <remarks>
/// Only an already-cancelled <see cref="CancellationToken"/> is surfaced as an exception, matching the
/// cancellation standard of the other RAG pipelines. Every other failure — a null request, a missing index
/// name, a non-positive top-k value, a request without retrievable query input, an embedding abstraction that
/// fails or produces an empty embedding, and a vector store query that fails or reports an unsuccessful
/// result — is returned as a failed <see cref="RetrievalResult"/> with a deterministic
/// <see cref="RetrievalErrorCode"/>, never as a provider-specific exception. Invalid requests are reported
/// with <see cref="RetrievalErrorCode.InvalidRequest"/> before any embedding or vector store abstraction is
/// invoked.
/// </remarks>
public interface IRagRetrievalPipeline
{
    /// <summary>
    /// Runs a single query-time retrieval: resolves a query vector for the request (embedding the query text
    /// when no pre-computed vector is supplied), queries the target vector index with the request's index name,
    /// top-k value, and metadata filter, and maps the store's matches into the standard retrieval result.
    /// </summary>
    /// <param name="request">The provider-independent retrieval request.</param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the operation. It is forwarded to both the embedding generation and
    /// the vector store query.
    /// </param>
    /// <returns>
    /// A successful result carrying the retrieved chunk content, metadata, and similarity scores, or a failed
    /// result with a provider-independent error category.
    /// </returns>
    Task<RetrievalResult> RetrieveAsync(
        RetrievalRequest request,
        CancellationToken cancellationToken = default);
}

