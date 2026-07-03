using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;
using Runiq.Rag.Models.VectorStores;

namespace Runiq.Rag.Abstractions.VectorStores;

/// <summary>
/// Defines a vector store that can persist vectors and perform similarity search operations.
/// </summary>
public interface IRagVectorStore
{
    /// <summary>
    /// Creates a provider-independent vector index.
    /// </summary>
    /// <param name="request">The vector index creation request.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The vector index creation result.</returns>
    Task<CreateVectorIndexResult> CreateIndexAsync(
        CreateVectorIndexRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates vectors in the vector store.
    /// </summary>
    /// <param name="request">The provider-independent vector upsert request.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The provider-independent vector upsert result.</returns>
    Task<UpsertVectorResult> UpsertAsync(
        UpsertVectorRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes vectors from the vector store by vector identifier.
    /// </summary>
    /// <param name="request">The provider-independent vector delete request.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The provider-independent vector delete result.</returns>
    Task<DeleteVectorResult> DeleteAsync(
        DeleteVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(new DeleteVectorResult
        {
            Succeeded = false,
            RequestedCount = request.VectorIds.Count,
            DeletedCount = 0,
            NotFoundVectorIds = request.VectorIds.ToList(),
            Reason = "Delete operation is not implemented by this vector store provider.",
        });
    }

    /// <summary>
    /// Queries the vector store for records that are similar to the supplied query vector. This is the retrieval
    /// entry point of the vector store contract: the search is isolated to the request's index, ordered best match
    /// first, and truncated to <see cref="QueryVectorRequest.TopK"/>. A query that matches no records is a successful,
    /// empty result rather than a failure. Similarity is reported as a provider-independent, higher-is-better score.
    /// </summary>
    /// <param name="request">The provider-independent vector query request.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The provider-independent vector query result.</returns>
    Task<QueryVectorResult> QueryAsync(
        QueryVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(new QueryVectorResult
        {
            Succeeded = false,
            Reason = "Query operation is not implemented by this vector store provider.",
        });
    }

    /// <summary>
    /// Inserts or updates a chunk and its embedding in the specified vector index.
    /// </summary>
    /// <param name="indexName">The vector index name that will receive the chunk vector.</param>
    /// <param name="chunk">The RAG chunk to store.</param>
    /// <param name="embedding">The embedding generated for the chunk content.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The provider-independent vector upsert result.</returns>
    Task<UpsertVectorResult> UpsertAsync(
        string indexName,
        RagChunk chunk,
        RagEmbedding embedding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(embedding);

        return UpsertAsync(
            new UpsertVectorRequest
            {
                IndexName = indexName,
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
    /// Searches the vector store for chunks that are similar to the specified query embedding.
    /// </summary>
    /// <param name="query">The RAG query that describes the retrieval request.</param>
    /// <param name="embedding">The embedding generated for the query text.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The matching RAG search results.</returns>
    Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        RagQuery query,
        RagEmbedding embedding,
        CancellationToken cancellationToken = default);

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
