using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.VectorStores;

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
                Score = record.Score,
                Metadata = CopyMetadata(record.Metadata),
            })
            .ToList();
    }

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

