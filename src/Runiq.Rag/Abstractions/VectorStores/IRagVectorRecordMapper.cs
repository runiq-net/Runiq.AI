using Runiq.Rag.Models.Ingestion;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.VectorStores;

namespace Runiq.Rag.Abstractions.VectorStores;

/// <summary>
/// Maps embedded RAG chunks into provider-independent vector records without performing vector store writes.
/// </summary>
public interface IRagVectorRecordMapper
{
    /// <summary>
    /// Creates one deterministic vector record for the embedded chunk association.
    /// </summary>
    /// <param name="item">The chunk and embedding association produced by document ingestion.</param>
    /// <param name="documentMetadata">Optional source document metadata to include when it is available.</param>
    /// <returns>A provider-independent vector record that can be used in an upsert request.</returns>
    VectorRecord Map(
        RagDocumentIngestionItem item,
        RagDocumentMetadata? documentMetadata = null);

    /// <summary>
    /// Creates deterministic vector records for embedded chunk associations while preserving input order.
    /// </summary>
    /// <param name="items">The chunk and embedding associations produced by document ingestion.</param>
    /// <param name="documentMetadata">Optional source document metadata to include when it is available.</param>
    /// <returns>Provider-independent vector records that can be used in an upsert request.</returns>
    IReadOnlyList<VectorRecord> MapMany(
        IEnumerable<RagDocumentIngestionItem> items,
        RagDocumentMetadata? documentMetadata = null);
}
