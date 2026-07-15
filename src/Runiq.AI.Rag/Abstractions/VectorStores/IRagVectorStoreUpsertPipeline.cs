using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.Abstractions.VectorStores;

/// <summary>
/// Orchestrates the provider-independent Vector Store upsert pipeline: it maps ingestion output or accepts
/// already-prepared vector records, runs dimension validation when expected dimensions are known, and writes
/// the resulting records to the target vector index through <see cref="IRagVectorStore"/>. Implementations
/// must remain provider-independent and must not perform document chunking, embedding generation, or contain
/// provider-specific vector store logic.
/// </summary>
/// <remarks>
/// Null arguments and an already-cancelled <see cref="CancellationToken"/> are programming errors and are
/// surfaced as exceptions. Every other failure — mapping failures, dimension validation failures, exceptions
/// raised by the underlying vector store, and a vector store result that reports
/// <see cref="UpsertVectorResult.Succeeded"/> as <see langword="false"/> without throwing — must be normalized
/// into a standard failed <see cref="UpsertVectorResult"/> without leaking any provider-specific exception type,
/// message, SDK detail, or raw provider result. A successful vector store result must be normalized the same
/// way, so the returned counts and partial-success fields never depend on what a specific vector store
/// implementation reports. Implementations do not support partial success: an upsert either fully succeeds for
/// every attempted record or is reported as a full failure.
/// </remarks>
public interface IRagVectorStoreUpsertPipeline
{
    /// <summary>
    /// Maps a RAG document ingestion result into vector records using the configured upsert request mapper,
    /// then starts a provider-independent upsert of those records into the specified vector index.
    /// </summary>
    /// <param name="ingestionResult">The document, chunk, and embedding output produced by the ingestion pipeline.</param>
    /// <param name="indexName">The target vector index that should receive the resulting vector records.</param>
    /// <param name="documentMetadata">Optional source document metadata to include when it is available.</param>
    /// <param name="expectedDimensions">
    /// The vector dimension count expected by the target index. When supplied, the pipeline validates every
    /// mapped vector record against this value before the vector store write is attempted.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that is checked for cancellation before the ingestion result is mapped. The mapper itself does
    /// not accept a cancellation token, so this token is not observed while mapping runs; it is forwarded to
    /// dimension validation and the vector store write once mapping has produced the upsert request.
    /// </param>
    /// <returns>The provider-independent outcome of the upsert pipeline.</returns>
    Task<UpsertVectorResult> UpsertAsync(
        RagDocumentIngestionResult ingestionResult,
        string indexName,
        RagDocumentMetadata? documentMetadata = null,
        int? expectedDimensions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a provider-independent upsert for an already-prepared vector store request. When the request
    /// specifies expected dimensions, every vector record is validated before the vector store write is attempted.
    /// </summary>
    /// <param name="request">The provider-independent upsert request containing the vector records to write.</param>
    /// <param name="cancellationToken">A token that can be used to cancel validation and the vector store write.</param>
    /// <returns>The provider-independent outcome of the upsert pipeline.</returns>
    Task<UpsertVectorResult> UpsertAsync(
        UpsertVectorRequest request,
        CancellationToken cancellationToken = default);
}

