using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;
using Runiq.Rag.Models.VectorStores;

namespace Runiq.Rag.VectorStores;

/// <summary>
/// Provides a safe no-op vector store that does not persist or retrieve chunks.
/// </summary>
public sealed class NullVectorStore : IRagVectorStore
{
    private const string InvalidIndexNameReason = "Vector index name is required.";

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
        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return Task.FromResult(new CreateVectorIndexResult
            {
                IndexName = request.IndexName ?? string.Empty,
                Succeeded = false,
                Reason = InvalidIndexNameReason,
            });
        }

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
        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return Task.FromResult(new UpsertVectorResult
            {
                Succeeded = false,
                Reason = InvalidIndexNameReason,
            });
        }

        return Task.FromResult(new UpsertVectorResult
        {
            Succeeded = true,
            UpsertedCount = request.Records?.Count ?? 0,
            VectorIds = request.Records?.Select(record => record.Id).ToList() ?? [],
        });
    }

    /// <summary>
    /// Completes successfully without deleting physical vector records.
    /// </summary>
    /// <param name="request">The vector delete request.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A successful vector delete result that treats every requested identifier as not found.</returns>
    public Task<DeleteVectorResult> DeleteAsync(
        DeleteVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return Task.FromResult(new DeleteVectorResult
            {
                Succeeded = false,
                RequestedCount = request.VectorIds?.Count ?? 0,
                NotFoundVectorIds = request.VectorIds?.ToList() ?? [],
                Reason = InvalidIndexNameReason,
            });
        }

        return Task.FromResult(new DeleteVectorResult
        {
            Succeeded = true,
            RequestedCount = request.VectorIds?.Count ?? 0,
            DeletedCount = 0,
            NotFoundVectorIds = request.VectorIds?.ToList() ?? [],
        });
    }

    /// <summary>
    /// Returns a successful empty vector query result without querying external services.
    /// </summary>
    /// <param name="request">The vector query request.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A successful empty vector query result.</returns>
    public Task<QueryVectorResult> QueryAsync(
        QueryVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return Task.FromResult(new QueryVectorResult
            {
                Succeeded = false,
                Reason = InvalidIndexNameReason,
            });
        }

        return Task.FromResult(new QueryVectorResult
        {
            Succeeded = true,
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

        return Task.FromResult(new UpsertVectorResult
        {
            Succeeded = true,
            UpsertedCount = 1,
            VectorIds = [chunk.Id],
        });
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
}
