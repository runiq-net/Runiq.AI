using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Embeddings;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.Retrieval;
using Runiq.Rag.VectorStores;

namespace Runiq.Rag.Tests.Retrieval;

public sealed class DefaultRetrieverTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenEmbeddingProviderIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRetriever(null!, new NullVectorStore()));

        Assert.Equal("embeddingProvider", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenVectorStoreIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRetriever(new NullEmbeddingProvider(), null!));

        Assert.Equal("vectorStore", exception.ParamName);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldCallEmbeddingProviderWithQueryText()
    {
        var embeddingProvider = new TrackingEmbeddingProvider(new RagEmbedding([1.0f]));
        var vectorStore = new TrackingVectorStore([]);
        var retriever = new DefaultRetriever(embeddingProvider, vectorStore);

        await retriever.RetrieveAsync(new RagQuery { Text = "search text" });

        Assert.Equal("search text", embeddingProvider.GeneratedText);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldCallVectorStoreWithGeneratedEmbedding()
    {
        var embedding = new RagEmbedding([1.0f, 2.0f]);
        var embeddingProvider = new TrackingEmbeddingProvider(embedding);
        var vectorStore = new TrackingVectorStore([]);
        var retriever = new DefaultRetriever(embeddingProvider, vectorStore);
        var query = new RagQuery { Text = "search text" };

        await retriever.RetrieveAsync(query);

        Assert.Same(query, vectorStore.SearchQuery);
        Assert.Same(embedding, vectorStore.SearchEmbedding);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnEmptyList_WhenUsingNullDependencies()
    {
        var retriever = new DefaultRetriever(
            new NullEmbeddingProvider(),
            new NullVectorStore());

        var results = await retriever.RetrieveAsync(new RagQuery { Text = "search text" });

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    private sealed class TrackingEmbeddingProvider : IRagEmbeddingProvider
    {
        private readonly RagEmbedding embedding;

        public TrackingEmbeddingProvider(RagEmbedding embedding)
        {
            this.embedding = embedding;
        }

        public string? GeneratedText { get; private set; }

        public Task<RagEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            GeneratedText = text;

            return Task.FromResult(embedding);
        }
    }

    private sealed class TrackingVectorStore : IRagVectorStore
    {
        private readonly IReadOnlyList<RagSearchResult> results;

        public TrackingVectorStore(IReadOnlyList<RagSearchResult> results)
        {
            this.results = results;
        }

        public RagQuery? SearchQuery { get; private set; }

        public RagEmbedding? SearchEmbedding { get; private set; }

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

        public Task UpsertAsync(
            RagChunk chunk,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            SearchQuery = query;
            SearchEmbedding = embedding;

            return Task.FromResult(results);
        }
    }
}
