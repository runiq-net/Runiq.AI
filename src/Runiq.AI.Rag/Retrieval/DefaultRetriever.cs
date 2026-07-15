using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Rag.Retrieval;

/// <summary>
/// Provides the default retrieval orchestration over an embedding provider and vector store.
/// </summary>
public sealed class DefaultRetriever : IRagRetriever
{
    private const string MissingIndexNameMessage = "RAG retrieval requires a non-empty vector index name. Set RagQuery.IndexName or RagOptions.DefaultIndexName.";

    private readonly IRagEmbeddingProvider embeddingProvider;
    private readonly IRagVectorStore vectorStore;
    private readonly RagOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRetriever"/> class.
    /// </summary>
    /// <param name="embeddingProvider">The embedding provider used to embed query text.</param>
    /// <param name="vectorStore">The vector store used to search for matching chunks.</param>
    /// <param name="options">The RAG options used to resolve default retrieval settings.</param>
    public DefaultRetriever(
        IRagEmbeddingProvider embeddingProvider,
        IRagVectorStore vectorStore,
        IOptions<RagOptions>? options = null)
    {
        this.embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        this.vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        this.options = options?.Value ?? new RagOptions();
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
        ArgumentNullException.ThrowIfNull(query);

        var indexName = ResolveIndexName(query);
        var embedding = await embeddingProvider.GenerateAsync(query.Text, cancellationToken).ConfigureAwait(false);
        var resolvedQuery = new RagQuery
        {
            Text = query.Text,
            IndexName = indexName,
            TopK = query.TopK,
            Metadata = query.Metadata,
        };

        return await vectorStore.SearchAsync(resolvedQuery, embedding, cancellationToken).ConfigureAwait(false);
    }

    private string ResolveIndexName(RagQuery query)
    {
        var indexName = query.IndexName ?? options.DefaultIndexName;

        if (string.IsNullOrWhiteSpace(indexName))
        {
            throw new InvalidOperationException(MissingIndexNameMessage);
        }

        return indexName.Trim();
    }
}

