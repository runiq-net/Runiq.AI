using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;
using Runiq.Rag.Models.VectorStores;

namespace Runiq.Rag.VectorStores;

/// <summary>
/// Provides a safe no-op vector store that does not persist or retrieve chunks.
/// </summary>
public sealed class NullVectorStore : IRagVectorStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullVectorStore"/> class.
    /// </summary>
    public NullVectorStore()
    {
    }

    /// <summary>
    /// Completes successfully without creating a physical vector index.
    /// </summary>
    /// <param name="request">The vector index creation request.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A successful vector index creation result.</returns>
    public Task<CreateVectorIndexResult> CreateIndexAsync(
        CreateVectorIndexRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CreateVectorIndexResult
        {
            IndexName = request.IndexName,
            Succeeded = true,
        });
    }

    /// <summary>
    /// Completes successfully without storing vector records.
    /// </summary>
    /// <param name="request">The vector upsert request.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A successful vector upsert result.</returns>
    public Task<UpsertVectorResult> UpsertAsync(
        UpsertVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UpsertVectorResult
        {
            Succeeded = true,
            UpsertedCount = request.Records.Count,
            VectorIds = request.Records.Select(record => record.Id).ToList(),
        });
    }

    /// <summary>
    /// Completes successfully without storing the chunk or embedding.
    /// </summary>
    /// <param name="chunk">The RAG chunk to store.</param>
    /// <param name="embedding">The embedding generated for the chunk content.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A successful vector upsert result.</returns>
    public Task<UpsertVectorResult> UpsertAsync(
        RagChunk chunk,
        RagEmbedding embedding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(embedding);

        return UpsertAsync(
            new UpsertVectorRequest
            {
                IndexName = string.Empty,
                Records =
                [
                    new VectorRecord
                    {
                        Id = chunk.Id,
                        Values = embedding.Values,
                        Content = chunk.Content,
                        Metadata = BuildChunkMetadata(chunk),
                    },
                ],
            },
            cancellationToken);
    }

    /// <summary>
    /// Returns an empty search result list without querying external services.
    /// </summary>
    /// <param name="query">The RAG query that describes the retrieval request.</param>
    /// <param name="embedding">The embedding generated for the query text.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>An empty list of RAG search results.</returns>
    public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        RagQuery query,
        RagEmbedding embedding,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<RagSearchResult>>(Array.Empty<RagSearchResult>());
    }

    private static RagMetadata BuildChunkMetadata(RagChunk chunk)
    {
        var values = new Dictionary<string, string>(chunk.Metadata.AdditionalMetadata.Values)
        {
            ["documentId"] = chunk.DocumentId,
            ["chunkIndex"] = chunk.Index.ToString(),
        };

        if (chunk.Metadata.StartIndex.HasValue)
        {
            values["startIndex"] = chunk.Metadata.StartIndex.Value.ToString();
        }

        if (chunk.Metadata.EndIndex.HasValue)
        {
            values["endIndex"] = chunk.Metadata.EndIndex.Value.ToString();
        }

        if (chunk.Metadata.TokenCount.HasValue)
        {
            values["tokenCount"] = chunk.Metadata.TokenCount.Value.ToString();
        }

        return new RagMetadata(values);
    }
}
