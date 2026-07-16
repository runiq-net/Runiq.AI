using Runiq.AI.Rag.Models.Ingestion;

namespace Runiq.AI.Rag.Abstractions.Ingestion;

/// <summary>Discovers source documents and supplies their content for RAG ingestion.</summary>
public interface IRagDocumentSource
{
    /// <summary>Gets the documents exposed by this source in deterministic order.</summary>
    /// <param name="cancellationToken">A token that can cancel discovery.</param>
    /// <returns>The source documents.</returns>
    Task<IReadOnlyList<RagSourceDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default);
}
