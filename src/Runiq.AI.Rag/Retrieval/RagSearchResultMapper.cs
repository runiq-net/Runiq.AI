using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.Retrieval;

internal static class RagSearchResultMapper
{
    public static IReadOnlyList<RagSearchResult> Map(IEnumerable<VectorSearchResult> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records
            .Select(record => new RagSearchResult
            {
                Chunk = new RagChunk
                {
                    Id = record.Id,
                    DocumentId = GetMetadataValue(record.Metadata, "documentId"),
                    Content = record.Content,
                    Index = ParseInt32(GetMetadataValue(record.Metadata, "chunkIndex")),
                    Metadata = new RagChunkMetadata
                    {
                        AdditionalMetadata = CopyMetadata(record.Metadata),
                    },
                },
                RawScore = record.RawScore,
                Relevance = record.Relevance,
                Metric = record.Metric,
                HigherIsBetter = record.HigherIsBetter,
                Metadata = CopyMetadata(record.Metadata),
            })
            .ToList();
    }

    public static IReadOnlyList<RagSearchResult> Map(IEnumerable<RetrievalResultItem> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records.Select(record => Create(
            record.RecordId, record.Content, record.Metadata, record.RawScore,
            record.Relevance, record.Metric, record.HigherIsBetter, record.Provenance)).ToList();
    }

    private static RagSearchResult Create(string id, string content, RagMetadata metadata, double? rawScore,
        double? relevance, string? metric, bool? higherIsBetter, RagRetrievalProvenance? provenance) => new()
    {
        Chunk = new RagChunk
        {
            Id = id,
            DocumentId = GetMetadataValue(metadata, "documentId"),
            Content = content,
            Index = ParseInt32(GetMetadataValue(metadata, "chunkIndex")),
            Metadata = new RagChunkMetadata { AdditionalMetadata = CopyMetadata(metadata) },
        },
        RawScore = rawScore,
        Relevance = relevance,
        Metric = metric,
        HigherIsBetter = higherIsBetter,
        Provenance = provenance,
        Metadata = CopyMetadata(metadata),
    };

    private static RagMetadata CopyMetadata(RagMetadata metadata)
    {
        return new RagMetadata(metadata.Values);
    }

    private static int ParseInt32(string? value)
    {
        return int.TryParse(value, out var result) ? result : 0;
    }

    private static string GetMetadataValue(RagMetadata metadata, string key)
    {
        return metadata.Values.TryGetValue(key, out var value) ? value : string.Empty;
    }
}

