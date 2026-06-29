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
    /// Completes successfully without storing the chunk or embedding.
    /// </summary>
    /// <param name="chunk">The RAG chunk to store.</param>
    /// <param name="embedding">The embedding generated for the chunk content.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task UpsertAsync(
        RagChunk chunk,
        RagEmbedding embedding,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
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
