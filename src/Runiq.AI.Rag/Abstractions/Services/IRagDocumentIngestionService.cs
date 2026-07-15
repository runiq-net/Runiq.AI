using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Ingestion;

namespace Runiq.AI.Rag.Abstractions.Services;

/// <summary>
/// Defines a provider-neutral service that ingests a RAG document into ordered chunks and chunk embeddings.
/// </summary>
public interface IRagDocumentIngestionService
{
    /// <summary>
    /// Runs the document ingestion flow by chunking the document and generating embeddings for each chunk.
    /// </summary>
    /// <param name="document">The source document to ingest.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The chunks and embedding results produced for the document.</returns>
    Task<RagDocumentIngestionResult> IngestAsync(
        RagDocument document,
        CancellationToken cancellationToken = default);
}

