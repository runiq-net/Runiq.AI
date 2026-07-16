using Runiq.AI.Rag.Models.Context;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Models.Ingestion;

namespace Runiq.AI.Rag.Abstractions.Services;

/// <summary>
/// Represents the high-level RAG service facade.
/// </summary>
public interface IRagService
{
    /// <summary>Discovers, parses, chunks, embeds, and persists documents from a source into an index.</summary>
    /// <param name="source">The document source to ingest.</param>
    /// <param name="indexName">The target vector index.</param>
    /// <param name="cancellationToken">A token that can cancel the run.</param>
    /// <returns>A structured ingestion report.</returns>
    Task<RagIngestionReport> IngestAsync(IRagDocumentSource source, string indexName, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This RAG service does not support document ingestion.");
    }

    /// <summary>Ingests one application-provided source document into an index.</summary>
    /// <param name="document">The source document.</param>
    /// <param name="indexName">The target vector index.</param>
    /// <param name="cancellationToken">A token that can cancel the run.</param>
    /// <returns>A structured ingestion report.</returns>
    Task<RagIngestionReport> IngestAsync(RagSourceDocument document, string indexName, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This RAG service does not support document ingestion.");
    }
    /// <summary>
    /// Gets assembled RAG context for the specified query.
    /// </summary>
    /// <param name="query">The query used to assemble context.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The assembled RAG context.</returns>
    Task<RagContext> GetContextAsync(
        RagQuery query,
        CancellationToken cancellationToken = default);
}

