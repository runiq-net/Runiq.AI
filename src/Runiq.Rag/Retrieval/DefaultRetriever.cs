using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Abstractions.Retrieval;
using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;

namespace Runiq.Rag.Retrieval;

/// <summary>
/// Provides the default retrieval orchestration over an embedding provider and vector store.
/// </summary>
public sealed class DefaultRetriever : IRagRetriever
{
    private readonly IRagEmbeddingProvider embeddingProvider;
    private readonly IRagVectorStore vectorStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRetriever"/> class.
    /// </summary>
    /// <param name="embeddingProvider">The embedding provider used to embed query text.</param>
    /// <param name="vectorStore">The vector store used to search for matching chunks.</param>
    public DefaultRetriever(
        IRagEmbeddingProvider embeddingProvider,
        IRagVectorStore vectorStore)
    {
        this.embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        this.vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
    }

    /// <summary>
    /// Retrieves relevant RAG search results by embedding the query text and searching the vector store.
    /// </summary>
    /// <param name="query">The retrieval query.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The retrieved RAG search results.</returns>
    public async Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
        RagQuery query,
        CancellationToken cancellationToken = default)
    {
        var embedding = await embeddingProvider.GenerateAsync(query.Text, cancellationToken).ConfigureAwait(false);
        var results = await vectorStore.SearchAsync(query, embedding, cancellationToken).ConfigureAwait(false);

        return results;
    }
}
