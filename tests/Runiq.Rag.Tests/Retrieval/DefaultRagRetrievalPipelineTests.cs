using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Retrieval;
using Runiq.Rag.Models.Search;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.Retrieval;

namespace Runiq.Rag.Tests.Retrieval;

public sealed class DefaultRagRetrievalPipelineTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenEmbeddingProviderIsNull()
    {
        // Verifies that the pipeline rejects a null embedding provider as a programming error.
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRagRetrievalPipeline(null!, new TrackingVectorStore()));

        Assert.Equal("embeddingProvider", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenVectorStoreIsNull()
    {
        // Verifies that the pipeline rejects a null vector store as a programming error.
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRagRetrievalPipeline(new TrackingEmbeddingProvider(), null!));

        Assert.Equal("vectorStore", exception.ParamName);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnInvalidRequestFailure_WhenRequestIsNull()
    {
        // Verifies that a null retrieval request returns a managed invalid request failure without calling embedding or vector store services.
        var embeddingProvider = new TrackingEmbeddingProvider();
        var vectorStore = new TrackingVectorStore();
        var pipeline = CreatePipeline(embeddingProvider, vectorStore);

        var result = await pipeline.RetrieveAsync(null!);

        Assert.False(embeddingProvider.WasCalled);
        Assert.False(vectorStore.QueryWasCalled);
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public async Task RetrieveAsync_ShouldSendQueryTextToEmbeddingAbstraction()
    {
        // Verifies that the retrieval pipeline forwards the request's query text to the embedding abstraction.
        var embeddingProvider = new TrackingEmbeddingProvider();
        var pipeline = CreatePipeline(embeddingProvider, new TrackingVectorStore());

        await pipeline.RetrieveAsync(CreateRequest(queryText: "how do I configure retrieval?"));

        Assert.True(embeddingProvider.WasCalled);
        Assert.Equal("how do I configure retrieval?", embeddingProvider.LastText);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldForwardQueryVectorIndexNameTopKAndMetadataFilterToVectorStore()
    {
        // Verifies that the retrieval pipeline forwards the query vector, index name, top-k value, and metadata filter to the vector store.
        var embeddingProvider = new TrackingEmbeddingProvider
        {
            ForcedEmbedding = new RagEmbedding([0.1f, 0.2f, 0.3f]),
        };
        var vectorStore = new TrackingVectorStore();
        var pipeline = CreatePipeline(embeddingProvider, vectorStore);
        var metadataFilter = new RetrievalMetadataFilter(new Dictionary<string, string>
        {
            ["source"] = "handbook",
        });
        var request = new RetrievalRequest
        {
            IndexName = "documents-index",
            QueryText = "query text",
            TopK = 7,
            MetadataFilter = metadataFilter,
        };

        await pipeline.RetrieveAsync(request);

        Assert.True(vectorStore.QueryWasCalled);
        Assert.Equal("documents-index", vectorStore.LastQueryRequest?.IndexName);
        Assert.Equal([0.1f, 0.2f, 0.3f], vectorStore.LastQueryRequest?.Values);
        Assert.Equal(7, vectorStore.LastQueryRequest?.TopK);
        Assert.Same(metadataFilter, vectorStore.LastQueryRequest?.MetadataFilter);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnRecordIdContentMetadataAndScoreFromVectorStoreResult()
    {
        // Verifies that the result carries the chunk content, metadata, and similarity score reported by the vector store query.
        var vectorStore = new TrackingVectorStore
        {
            ForcedResult = new QueryVectorResult
            {
                Succeeded = true,
                Records =
                [
                    new VectorSearchResult
                    {
                        Id = "document-1:chunk:0",
                        Content = "First chunk content.",
                        Score = 0.92,
                        Metadata = new RagMetadata(new Dictionary<string, string>
                        {
                            ["documentId"] = "document-1",
                        }),
                    },
                    new VectorSearchResult
                    {
                        Id = "document-1:chunk:1",
                        Content = "Second chunk content.",
                        Score = 0.81,
                    },
                ],
            },
        };
        var pipeline = CreatePipeline(new TrackingEmbeddingProvider(), vectorStore);

        var result = await pipeline.RetrieveAsync(CreateRequest());

        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("document-1:chunk:0", result.Items[0].RecordId);
        Assert.Equal("First chunk content.", result.Items[0].Content);
        Assert.Equal(0.92, result.Items[0].Score);
        Assert.Equal("document-1", result.Items[0].Metadata.Values["documentId"]);
        Assert.Equal("document-1:chunk:1", result.Items[1].RecordId);
        Assert.Equal(0.81, result.Items[1].Score);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnSuccessfulEmptyResult_WhenVectorStoreMatchesNothing()
    {
        // Verifies that a vector store query that matches nothing is a successful empty result, not a failure.
        var vectorStore = new TrackingVectorStore
        {
            ForcedResult = new QueryVectorResult
            {
                Succeeded = true,
                Records = [],
            },
        };
        var pipeline = CreatePipeline(new TrackingEmbeddingProvider(), vectorStore);

        var result = await pipeline.RetrieveAsync(CreateRequest());

        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldSkipEmbeddingGeneration_WhenRequestSuppliesPrecomputedQueryVector()
    {
        // Verifies that a pre-computed query vector is forwarded directly without invoking the embedding abstraction.
        var embeddingProvider = new TrackingEmbeddingProvider();
        var vectorStore = new TrackingVectorStore();
        var pipeline = CreatePipeline(embeddingProvider, vectorStore);
        var request = new RetrievalRequest
        {
            IndexName = "documents-index",
            QueryVector = [0.4f, 0.5f],
        };

        var result = await pipeline.RetrieveAsync(request);

        Assert.True(result.Succeeded);
        Assert.False(embeddingProvider.WasCalled);
        Assert.Equal([0.4f, 0.5f], vectorStore.LastQueryRequest?.Values);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnEmbeddingFailure_WithoutCallingVectorStore_WhenEmbeddingProviderThrows()
    {
        // Verifies that vector store query is not called when embedding generation fails.
        var vectorStore = new TrackingVectorStore();
        var pipeline = CreatePipeline(new ThrowingEmbeddingProvider(), vectorStore);

        var result = await pipeline.RetrieveAsync(CreateRequest());

        Assert.False(vectorStore.QueryWasCalled);
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.EmbeddingFailed, result.ErrorCode);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldNotLeakProviderDetails_WhenEmbeddingProviderThrows()
    {
        // Verifies that an embedding failure reason never surfaces provider-specific exception details.
        var pipeline = CreatePipeline(new ThrowingEmbeddingProvider(), new TrackingVectorStore());

        var result = await pipeline.RetrieveAsync(CreateRequest());

        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        Assert.DoesNotContain("OpenAI", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-live", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(FakeProviderSdkException), result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnEmbeddingFailure_WhenEmbeddingProviderReturnsEmptyEmbedding()
    {
        // Verifies that an empty embedding is treated as an embedding failure and the vector store is not queried.
        var embeddingProvider = new TrackingEmbeddingProvider
        {
            ForcedEmbedding = new RagEmbedding(),
        };
        var vectorStore = new TrackingVectorStore();
        var pipeline = CreatePipeline(embeddingProvider, vectorStore);

        var result = await pipeline.RetrieveAsync(CreateRequest());

        Assert.False(vectorStore.QueryWasCalled);
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.EmbeddingFailed, result.ErrorCode);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnVectorStoreQueryFailure_WhenVectorStoreThrows()
    {
        // Verifies that a vector store exception is normalized into a managed failure result without leaking provider details.
        var vectorStore = new ThrowingVectorStore();
        var pipeline = CreatePipeline(new TrackingEmbeddingProvider(), vectorStore);

        var result = await pipeline.RetrieveAsync(CreateRequest());

        Assert.True(vectorStore.QueryWasCalled);
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.VectorStoreQueryFailed, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        Assert.DoesNotContain("Pinecone", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-live", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(FakeProviderSdkException), result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnVectorStoreQueryFailure_WhenVectorStoreReportsUnsuccessfulResult()
    {
        // Verifies that an unsuccessful vector store result becomes a managed failure with a normalized, provider-independent reason.
        var rawProviderReason = "Qdrant gRPC status Unavailable: connection refused (10.0.0.5:6334).";
        var vectorStore = new TrackingVectorStore
        {
            ForcedResult = new QueryVectorResult
            {
                Succeeded = false,
                Reason = rawProviderReason,
            },
        };
        var pipeline = CreatePipeline(new TrackingEmbeddingProvider(), vectorStore);

        var result = await pipeline.RetrieveAsync(CreateRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.VectorStoreQueryFailed, result.ErrorCode);
        Assert.NotEqual(rawProviderReason, result.Reason);
        Assert.DoesNotContain("Qdrant", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("10.0.0.5", result.Reason, StringComparison.Ordinal);
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RetrieveAsync_ShouldReturnInvalidRequestFailure_WhenQueryTextIsMissing(string? queryText)
    {
        // Verifies that invalid retrieval requests fail before any provider abstraction is invoked.
        var embeddingProvider = new TrackingEmbeddingProvider();
        var vectorStore = new TrackingVectorStore();
        var pipeline = CreatePipeline(embeddingProvider, vectorStore);
        var request = new RetrievalRequest
        {
            IndexName = "documents-index",
            QueryText = queryText,
        };

        var result = await pipeline.RetrieveAsync(request);

        Assert.False(embeddingProvider.WasCalled);
        Assert.False(vectorStore.QueryWasCalled);
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RetrieveAsync_ShouldReturnInvalidRequestFailure_WhenIndexNameIsInvalid(string? indexName)
    {
        // Verifies that an invalid index name returns a managed failure result before any provider abstraction is invoked.
        var embeddingProvider = new TrackingEmbeddingProvider();
        var vectorStore = new TrackingVectorStore();
        var pipeline = CreatePipeline(embeddingProvider, vectorStore);
        var request = new RetrievalRequest
        {
            IndexName = indexName!,
            QueryText = "query text",
        };

        var result = await pipeline.RetrieveAsync(request);

        Assert.False(embeddingProvider.WasCalled);
        Assert.False(vectorStore.QueryWasCalled);
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RetrieveAsync_ShouldReturnInvalidRequestFailure_WhenTopKIsInvalid(int topK)
    {
        // Verifies that an invalid top-k value returns an invalid request result without calling embedding or vector store services.
        var embeddingProvider = new TrackingEmbeddingProvider();
        var vectorStore = new TrackingVectorStore();
        var pipeline = CreatePipeline(embeddingProvider, vectorStore);
        var request = new RetrievalRequest
        {
            IndexName = "documents-index",
            QueryText = "query text",
            TopK = topK,
        };

        var result = await pipeline.RetrieveAsync(request);

        Assert.False(embeddingProvider.WasCalled);
        Assert.False(vectorStore.QueryWasCalled);
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public async Task RetrieveAsync_ShouldPassCancellationTokenToEmbeddingProviderAndVectorStore()
    {
        // Verifies that the cancellation token is forwarded to both the embedding generation and the vector store query.
        var embeddingProvider = new TrackingEmbeddingProvider();
        var vectorStore = new TrackingVectorStore();
        var pipeline = CreatePipeline(embeddingProvider, vectorStore);
        using var cancellationTokenSource = new CancellationTokenSource();

        await pipeline.RetrieveAsync(CreateRequest(), cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, embeddingProvider.LastCancellationToken);
        Assert.Equal(cancellationTokenSource.Token, vectorStore.LastQueryCancellationToken);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldThrowBeforeCallingProviders_WhenCancellationIsAlreadyRequested()
    {
        // Verifies that an already-cancelled token throws before the embedding or vector store abstraction is invoked.
        var embeddingProvider = new TrackingEmbeddingProvider();
        var vectorStore = new TrackingVectorStore();
        var pipeline = CreatePipeline(embeddingProvider, vectorStore);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.RetrieveAsync(CreateRequest(), cancellationTokenSource.Token));

        Assert.False(embeddingProvider.WasCalled);
        Assert.False(vectorStore.QueryWasCalled);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldPropagateOperationCanceledException_WhenEmbeddingProviderCancels()
    {
        // Verifies that cancellation raised during embedding generation propagates instead of becoming a failure result.
        var pipeline = CreatePipeline(new CancellingEmbeddingProvider(), new TrackingVectorStore());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.RetrieveAsync(CreateRequest()));
    }

    [Fact]
    public async Task RetrieveAsync_ShouldPropagateOperationCanceledException_WhenVectorStoreCancels()
    {
        // Verifies that cancellation raised during the vector store query propagates instead of becoming a failure result.
        var pipeline = CreatePipeline(new TrackingEmbeddingProvider(), new CancellingVectorStore());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.RetrieveAsync(CreateRequest()));
    }

    private static DefaultRagRetrievalPipeline CreatePipeline(
        IRagEmbeddingProvider embeddingProvider,
        IRagVectorStore vectorStore)
    {
        return new DefaultRagRetrievalPipeline(embeddingProvider, vectorStore);
    }

    private static RetrievalRequest CreateRequest(string queryText = "query text")
    {
        return new RetrievalRequest
        {
            IndexName = "documents-index",
            QueryText = queryText,
        };
    }

    private sealed class FakeProviderSdkException : Exception
    {
        public FakeProviderSdkException(string message)
            : base(message)
        {
        }
    }

    private sealed class TrackingEmbeddingProvider : IRagEmbeddingProvider
    {
        public bool WasCalled { get; private set; }

        public string? LastText { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public RagEmbedding? ForcedEmbedding { get; set; }

        public Task<RagEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastText = text;
            LastCancellationToken = cancellationToken;

            return Task.FromResult(ForcedEmbedding ?? new RagEmbedding([0.1f, 0.2f]));
        }
    }

    private sealed class ThrowingEmbeddingProvider : IRagEmbeddingProvider
    {
        public Task<RagEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            throw new FakeProviderSdkException(
                "OpenAI API key rejected: sk-live-12345 at https://internal-embedding-host/embed");
        }
    }

    private sealed class CancellingEmbeddingProvider : IRagEmbeddingProvider
    {
        public Task<RagEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException();
        }
    }

    private class TrackingVectorStore : IRagVectorStore
    {
        public bool QueryWasCalled { get; private set; }

        public QueryVectorRequest? LastQueryRequest { get; private set; }

        public CancellationToken LastQueryCancellationToken { get; private set; }

        public QueryVectorResult? ForcedResult { get; set; }

        public Task<CreateVectorIndexResult> CreateIndexAsync(
            CreateVectorIndexRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vector store index creation should not be called by the retrieval pipeline.");
        }

        public Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vector store upsert should not be called by the retrieval pipeline.");
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Legacy vector store search should not be called by the retrieval pipeline.");
        }

        public Task<QueryVectorResult> QueryAsync(
            QueryVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            QueryWasCalled = true;
            LastQueryRequest = request;
            LastQueryCancellationToken = cancellationToken;

            return QueryCoreAsync(request, cancellationToken);
        }

        protected virtual Task<QueryVectorResult> QueryCoreAsync(
            QueryVectorRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ForcedResult ?? new QueryVectorResult
            {
                Succeeded = true,
                Records = [],
            });
        }
    }

    private sealed class ThrowingVectorStore : TrackingVectorStore
    {
        protected override Task<QueryVectorResult> QueryCoreAsync(
            QueryVectorRequest request,
            CancellationToken cancellationToken)
        {
            throw new FakeProviderSdkException(
                "Pinecone API key rejected: sk-live-12345 at https://internal-pinecone-host/query");
        }
    }

    private sealed class CancellingVectorStore : TrackingVectorStore
    {
        protected override Task<QueryVectorResult> QueryCoreAsync(
            QueryVectorRequest request,
            CancellationToken cancellationToken)
        {
            throw new OperationCanceledException();
        }
    }
}
