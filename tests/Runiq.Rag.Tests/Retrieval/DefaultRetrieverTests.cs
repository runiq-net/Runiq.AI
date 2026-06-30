using Microsoft.Extensions.Options;
using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Configuration;
using Runiq.Rag.Embeddings;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.Retrieval;
using Runiq.Rag.VectorStores;
using Runiq.Rag.VectorStores.InMemory;

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

        await retriever.RetrieveAsync(new RagQuery { Text = "search text", IndexName = "documents" });

        Assert.Equal("search text", embeddingProvider.GeneratedText);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldCallSearchAsyncWithIndexNameAndGeneratedEmbedding()
    {
        var embedding = new RagEmbedding([1.0f, 2.0f]);
        var embeddingProvider = new TrackingEmbeddingProvider(embedding);
        var vectorStore = new SearchOnlyTrackingVectorStore([
            new RagSearchResult
            {
                Chunk = new() { Id = "chunk-1", DocumentId = "document-1" },
                Score = 0.9,
            },
        ]);
        var retriever = new DefaultRetriever(embeddingProvider, vectorStore);
        var query = new RagQuery { Text = "search text", IndexName = "documents" };

        var results = await retriever.RetrieveAsync(query);

        Assert.True(vectorStore.SearchAsyncWasCalled);
        Assert.Single(results);
        Assert.NotNull(vectorStore.SearchQuery);
        Assert.NotSame(query, vectorStore.SearchQuery);
        Assert.Equal("documents", vectorStore.SearchQuery.IndexName);
        Assert.Same(embedding, vectorStore.SearchEmbedding);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldUseDefaultIndexName_WhenQueryIndexNameIsMissing()
    {
        var embeddingProvider = new TrackingEmbeddingProvider(new RagEmbedding([1.0f]));
        var vectorStore = new TrackingVectorStore([]);
        var retriever = new DefaultRetriever(
            embeddingProvider,
            vectorStore,
            Options.Create(new RagOptions { DefaultIndexName = "default-index" }));

        await retriever.RetrieveAsync(new RagQuery { Text = "search text" });

        Assert.Equal("default-index", vectorStore.SearchQuery!.IndexName);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldUseQueryIndexName_WhenDefaultIndexNameAlsoExists()
    {
        var embeddingProvider = new TrackingEmbeddingProvider(new RagEmbedding([1.0f]));
        var vectorStore = new TrackingVectorStore([]);
        var retriever = new DefaultRetriever(
            embeddingProvider,
            vectorStore,
            Options.Create(new RagOptions { DefaultIndexName = "default-index" }));

        await retriever.RetrieveAsync(new RagQuery { Text = "search text", IndexName = "query-index" });

        Assert.Equal("query-index", vectorStore.SearchQuery!.IndexName);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldPassTopKAndMetadataFilterToVectorStore()
    {
        var metadata = new RagMetadata(new Dictionary<string, string>
        {
            ["tenant"] = "north",
        });
        var embeddingProvider = new TrackingEmbeddingProvider(new RagEmbedding([1.0f]));
        var vectorStore = new TrackingVectorStore([]);
        var retriever = new DefaultRetriever(embeddingProvider, vectorStore);

        await retriever.RetrieveAsync(new RagQuery
        {
            Text = "search text",
            IndexName = "documents",
            TopK = 7,
            Metadata = metadata,
        });

        Assert.Equal(7, vectorStore.SearchQuery!.TopK);
        Assert.Same(metadata, vectorStore.SearchQuery.Metadata);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldFailDeterministically_WhenIndexNameIsMissing()
    {
        var retriever = new DefaultRetriever(
            new TrackingEmbeddingProvider(new RagEmbedding([1.0f])),
            new TrackingVectorStore([]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            retriever.RetrieveAsync(new RagQuery { Text = "search text" }));

        Assert.Contains("IndexName", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RetrieveAsync_ShouldFailDeterministically_WhenQueryIndexNameIsInvalid(string indexName)
    {
        var retriever = new DefaultRetriever(
            new TrackingEmbeddingProvider(new RagEmbedding([1.0f])),
            new TrackingVectorStore([]),
            Options.Create(new RagOptions { DefaultIndexName = "default-index" }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            retriever.RetrieveAsync(new RagQuery { Text = "search text", IndexName = indexName }));
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnEmptyList_WhenUsingNullDependencies()
    {
        var retriever = new DefaultRetriever(
            new NullEmbeddingProvider(),
            new NullVectorStore());

        var results = await retriever.RetrieveAsync(new RagQuery { Text = "search text", IndexName = "documents" });

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldPropagateVectorStoreQueryFailure()
    {
        var retriever = new DefaultRetriever(
            new TrackingEmbeddingProvider(new RagEmbedding([1.0f, 0.0f, 0.0f])),
            new InMemoryRagVectorStore());

        var exception = await Assert.ThrowsAsync<RagVectorStoreQueryException>(() =>
            retriever.RetrieveAsync(new RagQuery { Text = "search text", IndexName = "missing-index" }));

        Assert.Equal("Vector index has not been created.", exception.Reason);
        Assert.Equal("missing-index", exception.IndexName);
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

        public Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UpsertVectorResult
            {
                Succeeded = true,
                UpsertedCount = request.Records?.Count ?? 0,
            });
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

    private sealed class SearchOnlyTrackingVectorStore : IRagVectorStore
    {
        private readonly IReadOnlyList<RagSearchResult> results;

        public SearchOnlyTrackingVectorStore(IReadOnlyList<RagSearchResult> results)
        {
            this.results = results;
        }

        public bool SearchAsyncWasCalled { get; private set; }

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

        public Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UpsertVectorResult
            {
                Succeeded = true,
                UpsertedCount = request.Records?.Count ?? 0,
            });
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            SearchAsyncWasCalled = true;
            SearchQuery = query;
            SearchEmbedding = embedding;

            return Task.FromResult(results);
        }
    }
}
