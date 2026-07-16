using Microsoft.Extensions.Options;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.Retrieval;
using Runiq.AI.Rag.VectorStores;
using Runiq.AI.Rag.VectorStores.InMemory;

namespace Runiq.AI.Rag.Tests.Retrieval;

public sealed class DefaultRetrieverTests
{
    [Theory]
    [InlineData(RetrievalErrorCode.None)]
    [InlineData((RetrievalErrorCode)999)]
    // Ensures retrieval execution exceptions cannot represent success or an undefined public classification.
    public void RetrievalExecutionException_ShouldRejectInvalidFailureClassification(RetrievalErrorCode errorCode)
    {
        Assert.Throws<ArgumentException>(() => new RagRetrievalExecutionException("failure", errorCode));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenEmbeddingProviderIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRetriever((IEmbeddingClient)null!, new NullVectorStore()));

        Assert.Equal("embeddingClient", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenVectorStoreIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRetriever(new TrackingEmbeddingClient([1.0f]), null!));

        Assert.Equal("vectorStore", exception.ParamName);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldCallEmbeddingProviderWithQueryText()
    {
        var embeddingProvider = new TrackingEmbeddingClient([1.0f]);
        var vectorStore = new TrackingVectorStore([]);
        var retriever = new DefaultRetriever(embeddingProvider, vectorStore);

        await retriever.RetrieveAsync(new RagQuery { Text = "search text", IndexName = "documents" });

        Assert.Equal("search text", embeddingProvider.LastRequest!.Inputs.Single());
    }

    [Fact]
    public async Task RetrieveAsync_ShouldCallSearchAsyncWithIndexNameAndGeneratedEmbedding()
    {
        var embedding = new[] { 1.0f, 2.0f };
        var embeddingProvider = new TrackingEmbeddingClient(embedding);
        var vectorStore = new SearchOnlyTrackingVectorStore([
            new RagSearchResult
            {
                Chunk = new() { Id = "chunk-1", DocumentId = "document-1" },
                RawScore = 0.9,
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
        Assert.Equal(embedding, vectorStore.SearchEmbedding!.Values);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldUseDefaultIndexName_WhenQueryIndexNameIsMissing()
    {
        var embeddingProvider = new TrackingEmbeddingClient([1.0f]);
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
        var embeddingProvider = new TrackingEmbeddingClient([1.0f]);
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
        var embeddingProvider = new TrackingEmbeddingClient([1.0f]);
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
            new TrackingEmbeddingClient([1.0f]),
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
            new TrackingEmbeddingClient([1.0f]),
            new TrackingVectorStore([]),
            Options.Create(new RagOptions { DefaultIndexName = "default-index" }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            retriever.RetrieveAsync(new RagQuery { Text = "search text", IndexName = indexName }));
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnEmptyList_WhenUsingNullDependencies()
    {
        var retriever = new DefaultRetriever(
            new TrackingEmbeddingClient([]),
            new NullVectorStore());

        var results = await retriever.RetrieveAsync(new RagQuery { Text = "search text", IndexName = "documents" });

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    // Ensures vector-store exceptions are converted to the existing provider-independent classification.
    public async Task RetrieveAsync_ShouldClassifyVectorStoreQueryFailure()
    {
        var retriever = new DefaultRetriever(
            new TrackingEmbeddingClient([1.0f, 0.0f, 0.0f]),
            new InMemoryRagVectorStore());

        var exception = await Assert.ThrowsAsync<RagRetrievalExecutionException>(() =>
            retriever.RetrieveAsync(new RagQuery { Text = "search text", IndexName = "missing-index" }));

        Assert.Equal(RetrievalErrorCode.VectorStoreQueryFailed, exception.ErrorCode);
        Assert.IsType<RagVectorStoreQueryException>(exception.InnerException);
    }

    [Fact]
    // Ensures embedding-provider failures are converted to the existing provider-independent classification.
    public async Task RetrieveAsync_ShouldClassifyEmbeddingFailure()
    {
        var retriever = new DefaultRetriever(
            new ThrowingEmbeddingClient(),
            new TrackingVectorStore([]));

        var exception = await Assert.ThrowsAsync<RagRetrievalExecutionException>(() =>
            retriever.RetrieveAsync(new RagQuery { Text = "search text", IndexName = "documents" }));

        Assert.Equal(RetrievalErrorCode.EmbeddingFailed, exception.ErrorCode);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private sealed class TrackingEmbeddingClient : IEmbeddingClient
    {
        private readonly IReadOnlyList<float> vector;

        public TrackingEmbeddingClient(IReadOnlyList<float> vector)
        {
            this.vector = vector;
        }

        public EmbeddingRequest? LastRequest { get; private set; }

        public Task<EmbeddingResponse> EmbedAsync(
            EmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new EmbeddingResponse(request.Inputs.Select((_, index) => new EmbeddingResult(index, vector, vector.Count)).ToList()));
        }
    }

    private sealed class ThrowingEmbeddingClient : IEmbeddingClient
    {
        public Task<EmbeddingResponse> EmbedAsync(
            EmbeddingRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromException<EmbeddingResponse>(new InvalidOperationException("provider secret"));
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

