using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Ingestion;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.VectorStores;
using System.Globalization;

namespace Runiq.Rag.VectorStores;

/// <summary>
/// Prepares provider-independent vector records from embedded chunks for later vector store upsert requests.
/// </summary>
public sealed class DefaultRagVectorRecordMapper : IRagVectorRecordMapper
{
    /// <inheritdoc />
    public VectorRecord Map(
        RagDocumentIngestionItem item,
        RagDocumentMetadata? documentMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        var chunk = item.Chunk;
        var embeddingResult = item.EmbeddingResult;

        if (!string.Equals(chunk.Id, embeddingResult.ChunkId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Vector record mapping expected embedding result for chunk '{chunk.Id}' but received '{embeddingResult.ChunkId}'.");
        }

        if (!string.Equals(chunk.DocumentId, embeddingResult.DocumentId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Vector record mapping expected embedding result for document '{chunk.DocumentId}' but received '{embeddingResult.DocumentId}'.");
        }

        if (chunk.Index != embeddingResult.ChunkIndex)
        {
            throw new InvalidOperationException(
                $"Vector record mapping expected embedding result for chunk index {chunk.Index} but received {embeddingResult.ChunkIndex}.");
        }

        var values = GetRequiredVectorValues(embeddingResult.Embedding, chunk.Id);

        return new VectorRecord
        {
            Id = chunk.Id,
            Values = values,
            Content = chunk.Content,
            Metadata = BuildMetadata(item, documentMetadata),
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<VectorRecord> MapMany(
        IEnumerable<RagDocumentIngestionItem> items,
        RagDocumentMetadata? documentMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items.Select(item => Map(item, documentMetadata)).ToList();
    }

    private static IReadOnlyList<float> GetRequiredVectorValues(
        RagEmbedding embedding,
        string chunkId)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        if (embedding.Values is null || embedding.Values.Count == 0)
        {
            throw new InvalidOperationException(
                $"Vector record mapping requires a non-empty embedding vector for chunk '{chunkId}'.");
        }

        return embedding.Values;
    }

    private static RagMetadata BuildMetadata(
        RagDocumentIngestionItem item,
        RagDocumentMetadata? documentMetadata)
    {
        var chunk = item.Chunk;
        var values = new Dictionary<string, string>();

        AddDocumentMetadata(values, documentMetadata);

        foreach (var (key, value) in chunk.Metadata.AdditionalMetadata.Values)
        {
            values[key] = value;
        }

        // Canonical RAG fields are written last so source metadata cannot spoof vector record identity.
        values["documentId"] = chunk.DocumentId;
        values["chunkId"] = chunk.Id;
        values["chunkIndex"] = chunk.Index.ToString(CultureInfo.InvariantCulture);

        if (chunk.Metadata.StartIndex.HasValue)
        {
            values["startIndex"] = chunk.Metadata.StartIndex.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (chunk.Metadata.EndIndex.HasValue)
        {
            values["endIndex"] = chunk.Metadata.EndIndex.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (chunk.Metadata.TokenCount.HasValue)
        {
            values["tokenCount"] = chunk.Metadata.TokenCount.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new RagMetadata(values);
    }

    private static void AddDocumentMetadata(
        Dictionary<string, string> values,
        RagDocumentMetadata? documentMetadata)
    {
        if (documentMetadata is null)
        {
            return;
        }

        AddIfPresent(values, "sourceId", documentMetadata.SourceId);
        AddIfPresent(values, "sourceName", documentMetadata.SourceName);
        AddIfPresent(values, "sourceUri", documentMetadata.SourceUri);
        AddIfPresent(values, "contentType", documentMetadata.ContentType);
        AddIfPresent(values, "createdAt", documentMetadata.CreatedAt?.ToString("O", CultureInfo.InvariantCulture));
        AddIfPresent(values, "updatedAt", documentMetadata.UpdatedAt?.ToString("O", CultureInfo.InvariantCulture));

        foreach (var (key, value) in documentMetadata.AdditionalMetadata.Values)
        {
            values[key] = value;
        }
    }

    private static void AddIfPresent(
        Dictionary<string, string> values,
        string key,
        string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            values[key] = value;
        }
    }
}
