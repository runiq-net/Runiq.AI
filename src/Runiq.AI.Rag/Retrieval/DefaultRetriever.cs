using Microsoft.Extensions.Options;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.Models;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Embeddings;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Rag.Retrieval;

/// <summary>
/// Provides the default retrieval orchestration over an embedding provider and vector store.
/// </summary>
public sealed class DefaultRetriever : IRagRetriever
{
    private const string MissingIndexNameMessage = "RAG retrieval requires a non-empty vector index name. Set RagQuery.IndexName or RagOptions.DefaultIndexName.";

    private readonly IEmbeddingClient embeddingClient;
    private readonly IRagVectorStore vectorStore;
    private readonly RagOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRetriever"/> class.
    /// </summary>
    /// <param name="embeddingClient">The Core embedding client used to embed query text.</param>
    /// <param name="vectorStore">The vector store used to search for matching chunks.</param>
    /// <param name="options">The RAG options used to resolve default retrieval settings.</param>
    public DefaultRetriever(
        IEmbeddingClient embeddingClient,
        IRagVectorStore vectorStore,
        IOptions<RagOptions>? options = null)
    {
        this.embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
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
        var model = ResolveEmbeddingModel();
        var response = await embeddingClient.EmbedAsync(new EmbeddingRequest(model, [query.Text], Dimensions: model.EmbeddingDimensions), cancellationToken).ConfigureAwait(false);
        var embedding = new Models.Embeddings.RagEmbedding(response.Results.Single().Vector);
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

    private ModelReference ResolveEmbeddingModel()
    {
        if (string.IsNullOrWhiteSpace(options.EmbeddingModel)) return ModelReference.Parse("openai/rag-embedding");
        return ProviderModelReferenceResolver.Resolve(ModelReference.Parse(options.EmbeddingModel), options.EmbeddingProvider);
    }
}

