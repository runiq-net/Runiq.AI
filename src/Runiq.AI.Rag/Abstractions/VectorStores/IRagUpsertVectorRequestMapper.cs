using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.Abstractions.VectorStores;

/// <summary>
/// Maps the provider-neutral output of a RAG document ingestion pipeline into a vector store upsert request.
/// Implementations must remain provider-independent: they only reshape ingestion output into the upsert
/// request contract and must not perform vector store writes, embedding generation, chunking, or dimension validation.
/// </summary>
public interface IRagUpsertVectorRequestMapper
{
    /// <summary>
    /// Builds an upsert request that carries one vector record per embedded chunk association from the
    /// ingestion result, targeting the specified vector index, while preserving the document, chunk,
    /// order, and source metadata produced during ingestion.
    /// </summary>
    /// <param name="ingestionResult">The document, chunk, and embedding output produced by the ingestion pipeline.</param>
    /// <param name="indexName">The target vector index that should receive the resulting vector records.</param>
    /// <param name="documentMetadata">Optional source document metadata to include when it is available.</param>
    /// <returns>A provider-independent upsert request ready for the vector store upsert pipeline.</returns>
    UpsertVectorRequest Map(
        RagDocumentIngestionResult ingestionResult,
        string indexName,
        RagDocumentMetadata? documentMetadata = null);
}

