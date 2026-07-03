using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Ingestion;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.VectorStores;

namespace Runiq.Rag.VectorStores;

/// <summary>
/// Converts a completed RAG document ingestion result into a provider-independent vector store upsert
/// request. This mapper reuses <see cref="IRagVectorRecordMapper"/> to build the individual vector records
/// so that the document/chunk/embedding-to-metadata mapping logic is defined in exactly one place. It is
/// responsible only for reshaping ingestion output into the <see cref="UpsertVectorRequest"/> contract and
/// performs no vector store writes, embedding generation, chunking, or dimension validation.
/// </summary>
public sealed class DefaultRagUpsertVectorRequestMapper : IRagUpsertVectorRequestMapper
{
    private readonly IRagVectorRecordMapper vectorRecordMapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRagUpsertVectorRequestMapper"/> class.
    /// </summary>
    /// <param name="vectorRecordMapper">The mapper used to convert individual embedded chunk associations into vector records.</param>
    public DefaultRagUpsertVectorRequestMapper(IRagVectorRecordMapper vectorRecordMapper)
    {
        this.vectorRecordMapper = vectorRecordMapper ?? throw new ArgumentNullException(nameof(vectorRecordMapper));
    }

    /// <inheritdoc />
    public UpsertVectorRequest Map(
        RagDocumentIngestionResult ingestionResult,
        string indexName,
        RagDocumentMetadata? documentMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(ingestionResult);

        // Only chunks with a paired embedding result appear in Items, so a chunk that failed embedding
        // is deterministically excluded from the resulting vector records without special-casing it here.
        var records = vectorRecordMapper.MapMany(ingestionResult.Items, documentMetadata);

        return new UpsertVectorRequest
        {
            IndexName = indexName,
            Records = records.ToList(),
        };
    }
}
