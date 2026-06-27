using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;

namespace Runiq.Rag.Abstractions.VectorStores;

/// <summary>
/// Defines a vector store that can persist embedded RAG chunks and perform similarity search operations.
/// </summary>
public interface IRagVectorStore
{
    /// <summary>
    /// Inserts or updates a chunk and its embedding in the vector store.
    /// </summary>
    /// <param name="chunk">The RAG chunk to store.</param>
    /// <param name="embedding">The embedding generated for the chunk content.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task UpsertAsync(
        RagChunk chunk,
        RagEmbedding embedding,
        CancellationToken cancellationToken = default);

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
}
