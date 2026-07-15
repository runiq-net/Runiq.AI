using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Telemetry;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.Retrieval;
using Runiq.AI.Rag.Telemetry;
using Runiq.AI.Rag.Tests.TestDoubles;
using Runiq.AI.Rag.VectorStores;

namespace Runiq.AI.Rag.Tests.Telemetry;

public sealed class RagPipelineTelemetryRecordingTests
{
    private static readonly DateTimeOffset RecordingTime = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RetrieveAsync_ShouldRecordSuccessMetrics_WithDeterministicDuration()
    {
        // Verifies that a successful retrieval records the result count, a duration measured through the
        // injected deterministic clock, and the recording timestamp, without changing the result itself.
        var timeProvider = new FakeTimeProvider(RecordingTime, stepPerTimestampCall: TimeSpan.FromSeconds(5));
        var recorder = new DefaultRagOperationTelemetryRecorder(timeProvider);
        var pipeline = new DefaultRagRetrievalPipeline(
            new RecordingEmbeddingClient(dimensions: 2),
            new QueryVectorStore(CreateQueryResult("chunk-1", "chunk-2")),
            recorder,
            timeProvider);

        var result = await pipeline.RetrieveAsync(CreateRetrievalRequest());

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Items.Count);

        var snapshot = recorder.LastRetrieval;
        Assert.NotNull(snapshot);
        Assert.True(snapshot.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, snapshot.ErrorCode);
        Assert.Equal(2, snapshot.ResultCount);
        Assert.Equal(TimeSpan.FromSeconds(5), snapshot.Duration);
        Assert.Equal(RecordingTime, snapshot.Timestamp);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldRecordFailureMetrics_WhenVectorStoreThrows()
    {
        // Verifies that a retrieval failure is recorded with its deterministic error code and reason
        // while the returned failure result stays exactly as the pipeline contract defines it.
        var timeProvider = new FakeTimeProvider(RecordingTime, stepPerTimestampCall: TimeSpan.FromSeconds(1));
        var recorder = new DefaultRagOperationTelemetryRecorder(timeProvider);
        var pipeline = new DefaultRagRetrievalPipeline(
            new RecordingEmbeddingClient(dimensions: 2),
            new ThrowingQueryVectorStore(),
            recorder,
            timeProvider);

        var result = await pipeline.RetrieveAsync(CreateRetrievalRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.VectorStoreQueryFailed, result.ErrorCode);

        var snapshot = recorder.LastRetrieval;
        Assert.NotNull(snapshot);
        Assert.False(snapshot.Succeeded);
        Assert.Equal(RetrievalErrorCode.VectorStoreQueryFailed, snapshot.ErrorCode);
        Assert.Equal(result.Reason, snapshot.Reason);
        Assert.Equal(0, snapshot.ResultCount);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldNotRecord_WhenCancellationPropagates()
    {
        // Verifies that cancellation surfaced as an exception bypasses recording, because no retrieval
        // result was produced for the telemetry snapshot.
        var recorder = new DefaultRagOperationTelemetryRecorder(new FakeTimeProvider(RecordingTime, TimeSpan.Zero));
        var pipeline = new DefaultRagRetrievalPipeline(
            new RecordingEmbeddingClient(dimensions: 2),
            new CancellingQueryVectorStore(),
            recorder,
            new FakeTimeProvider(RecordingTime, TimeSpan.Zero));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.RetrieveAsync(CreateRetrievalRequest()));

        Assert.Null(recorder.LastRetrieval);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnUnchangedResult_WhenRecorderThrows()
    {
        // Verifies the strictly observational guarantee: a recorder that throws never alters the
        // retrieval result or turns a successful retrieval into a failure.
        var pipeline = new DefaultRagRetrievalPipeline(
            new RecordingEmbeddingClient(dimensions: 2),
            new QueryVectorStore(CreateQueryResult("chunk-1")),
            new ThrowingTelemetryRecorder());

        var result = await pipeline.RetrieveAsync(CreateRetrievalRequest());

        Assert.True(result.Succeeded);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task UpsertAsync_ShouldRecordSuccessOutcome_WithChunkCount()
    {
        // Verifies that a successful upsert records the processed chunk count and the recording
        // timestamp from the injected deterministic clock.
        var recorder = new DefaultRagOperationTelemetryRecorder(new FakeTimeProvider(RecordingTime, TimeSpan.Zero));
        var pipeline = CreateUpsertPipeline(new SucceedingUpsertVectorStore(), recorder);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]),
            CreateItem("document-1", "document-1:chunk:1", 1, [0.2f]),
            CreateItem("document-1", "document-1:chunk:2", 2, [0.3f]));

        var result = await pipeline.UpsertAsync(ingestionResult, "documents-index");

        Assert.True(result.Succeeded);

        var snapshot = recorder.LastUpsert;
        Assert.NotNull(snapshot);
        Assert.True(snapshot.Succeeded);
        Assert.Equal(VectorStoreUpsertErrorCode.None, snapshot.ErrorCode);
        Assert.Equal(3, snapshot.ChunkCount);
        Assert.Equal(RecordingTime, snapshot.Timestamp);
    }

    [Fact]
    public async Task UpsertAsync_ShouldRecordStoreFailureOutcome_WithAttemptedChunkCount()
    {
        // Verifies that a vector store failure is recorded with the StoreFailed error code, the
        // pipeline's normalized reason, and the attempted record count as the chunk count.
        var recorder = new DefaultRagOperationTelemetryRecorder(new FakeTimeProvider(RecordingTime, TimeSpan.Zero));
        var pipeline = CreateUpsertPipeline(new ThrowingUpsertVectorStore(), recorder);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]),
            CreateItem("document-1", "document-1:chunk:1", 1, [0.2f]));

        var result = await pipeline.UpsertAsync(ingestionResult, "documents-index");

        Assert.False(result.Succeeded);

        var snapshot = recorder.LastUpsert;
        Assert.NotNull(snapshot);
        Assert.False(snapshot.Succeeded);
        Assert.Equal(VectorStoreUpsertErrorCode.StoreFailed, snapshot.ErrorCode);
        Assert.Equal(result.Reason, snapshot.Reason);
        Assert.Equal(2, snapshot.ChunkCount);
    }

    [Fact]
    public async Task UpsertAsync_ShouldRecordMappingFailureOutcome_FromIngestionOverload()
    {
        // Verifies that the ingestion-result overload's mapping failure boundary is also recorded,
        // carrying the MappingFailed error code and the attempted ingestion item count.
        var recorder = new DefaultRagOperationTelemetryRecorder(new FakeTimeProvider(RecordingTime, TimeSpan.Zero));
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(
            new ThrowingUpsertVectorRequestMapper(),
            new DefaultRagVectorRecordDimensionValidator(),
            new SucceedingUpsertVectorStore(),
            recorder);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));

        var result = await pipeline.UpsertAsync(ingestionResult, "documents-index");

        Assert.False(result.Succeeded);
        Assert.Equal(VectorStoreUpsertErrorCode.MappingFailed, result.ErrorCode);

        var snapshot = recorder.LastUpsert;
        Assert.NotNull(snapshot);
        Assert.Equal(VectorStoreUpsertErrorCode.MappingFailed, snapshot.ErrorCode);
        Assert.Equal(1, snapshot.ChunkCount);
    }

    [Fact]
    public async Task UpsertAsync_ShouldReturnUnchangedResult_WhenRecorderThrows()
    {
        // Verifies the strictly observational guarantee for the upsert pipeline: a throwing recorder
        // never alters the upsert result.
        var pipeline = CreateUpsertPipeline(new SucceedingUpsertVectorStore(), new ThrowingTelemetryRecorder());
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));

        var result = await pipeline.UpsertAsync(ingestionResult, "documents-index");

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.ProcessedCount);
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRecorderAndReader_AsSameSingletonInstance()
    {
        // Verifies that the recorder written by the pipelines and the reader consumed by visibility
        // features are the same singleton, so recorded snapshots are observable through the reader.
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var provider = services.BuildServiceProvider();

        var recorder = provider.GetRequiredService<IRagOperationTelemetryRecorder>();
        var reader = provider.GetRequiredService<IRagOperationTelemetryReader>();

        Assert.Same(recorder, reader);
        Assert.IsType<DefaultRagOperationTelemetryRecorder>(recorder);
    }

    [Fact]
    public void AddRuniqRag_ShouldPreserveHostTelemetryRecorderRegistration()
    {
        // Verifies the dependency injection override point: a recorder registered by the host before
        // AddRuniqRag is preserved instead of being replaced by the default recorder.
        var services = new ServiceCollection();
        var customRecorder = new ThrowingTelemetryRecorder();
        services.AddSingleton<IRagOperationTelemetryRecorder>(customRecorder);

        services.AddRuniqRag();

        using var provider = services.BuildServiceProvider();

        Assert.Same(customRecorder, provider.GetRequiredService<IRagOperationTelemetryRecorder>());
    }

    [Fact]
    public async Task AddRuniqRag_ShouldWireRecorderIntoResolvedPipelines()
    {
        // Verifies end-to-end DI wiring: pipelines resolved from the container record into the reader
        // without any additional host configuration. A controlled empty Core result makes retrieval
        // fail deterministically, which is still a recordable outcome.
        var services = new ServiceCollection();
        var embeddingClient = new RecordingEmbeddingClient(
            dimensions: 1,
            responseFactory: _ => new EmbeddingResponse([new EmbeddingResult(0, [], 0)]));
        services.AddRuniqRag();
        services.AddRagEmbeddingClient(_ => embeddingClient);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var retrievalPipeline = scope.ServiceProvider.GetRequiredService<IRagRetrievalPipeline>();
        var upsertPipeline = scope.ServiceProvider.GetRequiredService<IRagVectorStoreUpsertPipeline>();
        var reader = scope.ServiceProvider.GetRequiredService<IRagOperationTelemetryReader>();

        await retrievalPipeline.RetrieveAsync(CreateRetrievalRequest());
        await upsertPipeline.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents-index",
            Records = [new VectorRecord { Id = "chunk-1", Values = [0.1f] }],
        });

        Assert.NotNull(reader.LastRetrieval);
        Assert.Equal(RetrievalErrorCode.EmbeddingFailed, reader.LastRetrieval.ErrorCode);
        Assert.NotNull(reader.LastUpsert);
        Assert.Equal(1, reader.LastUpsert.ChunkCount);
    }

    private static DefaultRagVectorStoreUpsertPipeline CreateUpsertPipeline(
        IRagVectorStore vectorStore,
        IRagOperationTelemetryRecorder recorder)
    {
        return new DefaultRagVectorStoreUpsertPipeline(
            new DefaultRagUpsertVectorRequestMapper(new DefaultRagVectorRecordMapper()),
            new DefaultRagVectorRecordDimensionValidator(),
            vectorStore,
            recorder);
    }

    private static RetrievalRequest CreateRetrievalRequest()
    {
        return new RetrievalRequest
        {
            IndexName = "documents-index",
            QueryText = "query text",
        };
    }

    private static QueryVectorResult CreateQueryResult(params string[] recordIds)
    {
        return new QueryVectorResult
        {
            Succeeded = true,
            Records = recordIds
                .Select(recordId => new VectorSearchResult
                {
                    Id = recordId,
                    Content = "Chunk content.",
                    Score = 0.9,
                })
                .ToList(),
        };
    }

    private static RagDocumentIngestionResult CreateIngestionResult(params RagDocumentIngestionItem[] items)
    {
        return new RagDocumentIngestionResult
        {
            DocumentId = items[0].Chunk.DocumentId,
            Chunks = items.Select(item => item.Chunk).ToList(),
            Items = items,
        };
    }

    private static RagDocumentIngestionItem CreateItem(
        string documentId,
        string chunkId,
        int chunkIndex,
        IReadOnlyList<float> vectorValues)
    {
        var chunk = new RagChunk
        {
            Id = chunkId,
            DocumentId = documentId,
            Content = "Chunk content.",
            Index = chunkIndex,
        };

        return new RagDocumentIngestionItem
        {
            Chunk = chunk,
            EmbeddingResult = new RagChunkEmbeddingResult
            {
                ChunkId = chunk.Id,
                DocumentId = chunk.DocumentId,
                ChunkIndex = chunk.Index,
                Embedding = new RagEmbedding(vectorValues),
            },
        };
    }

    /// <summary>
    /// Deterministic time source for tests: a fixed wall-clock value and a monotonic timestamp that
    /// advances by a configured step on every timestamp call, using tick-based frequency so measured
    /// durations are exact.
    /// </summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;
        private readonly long stepTicks;
        private long currentTimestamp;

        public FakeTimeProvider(DateTimeOffset utcNow, TimeSpan stepPerTimestampCall)
        {
            this.utcNow = utcNow;
            stepTicks = stepPerTimestampCall.Ticks;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            var timestamp = currentTimestamp;
            currentTimestamp += stepTicks;

            return timestamp;
        }
    }

    private class QueryVectorStore : IRagVectorStore
    {
        private readonly QueryVectorResult queryResult;

        public QueryVectorStore(QueryVectorResult queryResult)
        {
            this.queryResult = queryResult;
        }

        public Task<CreateVectorIndexResult> CreateIndexAsync(
            CreateVectorIndexRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vector store index creation should not be called by these tests.");
        }

        public Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vector store upsert should not be called by the retrieval pipeline.");
        }

        public virtual Task<QueryVectorResult> QueryAsync(
            QueryVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(queryResult);
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Legacy vector store search should not be called by these tests.");
        }
    }

    private sealed class ThrowingQueryVectorStore : QueryVectorStore
    {
        public ThrowingQueryVectorStore()
            : base(new QueryVectorResult())
        {
        }

        public override Task<QueryVectorResult> QueryAsync(
            QueryVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Provider SDK rejected the query.");
        }
    }

    private sealed class CancellingQueryVectorStore : QueryVectorStore
    {
        public CancellingQueryVectorStore()
            : base(new QueryVectorResult())
        {
        }

        public override Task<QueryVectorResult> QueryAsync(
            QueryVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException();
        }
    }

    private class SucceedingUpsertVectorStore : IRagVectorStore
    {
        public Task<CreateVectorIndexResult> CreateIndexAsync(
            CreateVectorIndexRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vector store index creation should not be called by these tests.");
        }

        public virtual Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UpsertVectorResult
            {
                Succeeded = true,
                UpsertedCount = request.Records.Count,
                VectorIds = request.Records.Select(record => record.Id).ToList(),
            });
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vector store search should not be called by the upsert pipeline.");
        }
    }

    private sealed class ThrowingUpsertVectorStore : SucceedingUpsertVectorStore
    {
        public override Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Provider SDK rejected the upsert.");
        }
    }

    private sealed class ThrowingUpsertVectorRequestMapper : IRagUpsertVectorRequestMapper
    {
        public UpsertVectorRequest Map(
            RagDocumentIngestionResult ingestionResult,
            string indexName,
            RagDocumentMetadata? documentMetadata = null)
        {
            throw new InvalidOperationException("Vector record mapping failed for these tests.");
        }
    }

    private sealed class ThrowingTelemetryRecorder : IRagOperationTelemetryRecorder
    {
        public void RecordUpsert(UpsertVectorResult result)
        {
            throw new InvalidOperationException("Telemetry recorder failure.");
        }

        public void RecordRetrieval(RetrievalResult result, TimeSpan duration)
        {
            throw new InvalidOperationException("Telemetry recorder failure.");
        }
    }
}

