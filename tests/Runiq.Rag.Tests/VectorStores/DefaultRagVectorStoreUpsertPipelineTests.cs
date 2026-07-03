using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Ingestion;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.VectorStores;

namespace Runiq.Rag.Tests.VectorStores;

public sealed class DefaultRagVectorStoreUpsertPipelineTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenUpsertVectorRequestMapperIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRagVectorStoreUpsertPipeline(null!, new TrackingDimensionValidator(), new TrackingVectorStore()));

        Assert.Equal("upsertVectorRequestMapper", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDimensionValidatorIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRagVectorStoreUpsertPipeline(CreateMapper(), null!, new TrackingVectorStore()));

        Assert.Equal("dimensionValidator", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenVectorStoreIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRagVectorStoreUpsertPipeline(CreateMapper(), new TrackingDimensionValidator(), null!));

        Assert.Equal("vectorStore", exception.ParamName);
    }

    [Fact]
    public async Task UpsertAsync_ShouldCallVectorStore_WithValidIngestionOutput()
    {
        var mapper = CreateMapper();
        var vectorStore = new TrackingVectorStore();
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, new TrackingDimensionValidator(), vectorStore);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f, 0.2f]));

        await pipeline.UpsertAsync(ingestionResult, "documents-index");

        Assert.True(vectorStore.UpsertWasCalled);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUseRequestedIndexNameWhenCallingVectorStore()
    {
        var mapper = CreateMapper();
        var vectorStore = new TrackingVectorStore();
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, new TrackingDimensionValidator(), vectorStore);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));

        await pipeline.UpsertAsync(ingestionResult, "tenant-a-index");

        Assert.Equal("tenant-a-index", vectorStore.LastRequest?.IndexName);
    }

    [Fact]
    public async Task UpsertAsync_ShouldWriteMultipleChunkEmbeddingsToSameIndex()
    {
        var mapper = CreateMapper();
        var vectorStore = new TrackingVectorStore();
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, new TrackingDimensionValidator(), vectorStore);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]),
            CreateItem("document-1", "document-1:chunk:1", 1, [0.2f]),
            CreateItem("document-1", "document-1:chunk:2", 2, [0.3f]));

        var result = await pipeline.UpsertAsync(ingestionResult, "documents-index");

        Assert.Equal(3, vectorStore.LastRequest?.Records.Count);
        Assert.Equal("documents-index", vectorStore.LastRequest?.IndexName);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUseMapperToBuildRequestFromIngestionOutput()
    {
        var mapper = CreateMapper();
        var vectorStore = new TrackingVectorStore();
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, new TrackingDimensionValidator(), vectorStore);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));
        var documentMetadata = new RagDocumentMetadata
        {
            SourceName = "handbook.md",
        };

        await pipeline.UpsertAsync(ingestionResult, "documents-index", documentMetadata);

        Assert.True(mapper.WasCalled);
        Assert.Same(ingestionResult, mapper.LastIngestionResult);
        Assert.Equal("documents-index", mapper.LastIndexName);
        Assert.Same(documentMetadata, mapper.LastDocumentMetadata);
    }

    [Fact]
    public async Task UpsertAsync_ShouldCallDimensionValidator_WhenExpectedDimensionsProvided()
    {
        var mapper = CreateMapper();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new TrackingVectorStore();
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, validator, vectorStore);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f, 0.2f]));

        await pipeline.UpsertAsync(ingestionResult, "documents-index", expectedDimensions: 2);

        Assert.True(validator.WasCalled);
        Assert.Equal(2, validator.LastExpectedDimensions);
        Assert.Equal("documents-index", validator.LastRequest?.IndexName);
    }

    [Fact]
    public async Task UpsertAsync_ShouldNotCallDimensionValidator_WhenExpectedDimensionsAreNotProvided()
    {
        var mapper = CreateMapper();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new TrackingVectorStore();
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, validator, vectorStore);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));

        await pipeline.UpsertAsync(ingestionResult, "documents-index");

        Assert.False(validator.WasCalled);
        Assert.True(vectorStore.UpsertWasCalled);
    }

    [Fact]
    public async Task UpsertAsync_ShouldNotCallVectorStore_WhenDimensionValidationFails()
    {
        var mapper = CreateMapper();
        var validator = new TrackingDimensionValidator
        {
            ForcedResult = new VectorRecordDimensionValidationResult
            {
                Succeeded = false,
                Reason = "Vector dimension does not match the index dimensions.",
                IndexName = "documents-index",
                RecordId = "document-1:chunk:0",
                ExpectedDimensions = 3,
                ActualDimensions = 1,
            },
        };
        var vectorStore = new TrackingVectorStore();
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, validator, vectorStore);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));

        var result = await pipeline.UpsertAsync(ingestionResult, "documents-index", expectedDimensions: 3);

        Assert.False(vectorStore.UpsertWasCalled);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UpsertAsync_ShouldReflectDimensionValidationFailureInResult()
    {
        var mapper = CreateMapper();
        var validator = new TrackingDimensionValidator
        {
            ForcedResult = new VectorRecordDimensionValidationResult
            {
                Succeeded = false,
                Reason = "Vector dimension does not match the index dimensions.",
                IndexName = "documents-index",
                RecordId = "document-1:chunk:0",
                ExpectedDimensions = 3,
                ActualDimensions = 1,
            },
        };
        var vectorStore = new TrackingVectorStore();
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, validator, vectorStore);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));

        var result = await pipeline.UpsertAsync(ingestionResult, "documents-index", expectedDimensions: 3);

        Assert.Equal("Vector dimension does not match the index dimensions.", result.Reason);
        Assert.Equal("documents-index", result.IndexName);
        Assert.Equal("document-1:chunk:0", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(1, result.ActualDimensions);
    }

    [Fact]
    public async Task UpsertAsync_ShouldReflectVectorStoreSuccessResult()
    {
        var mapper = CreateMapper();
        var vectorStore = new TrackingVectorStore
        {
            ForcedResult = new UpsertVectorResult
            {
                Succeeded = true,
                UpsertedCount = 1,
                VectorIds = ["document-1:chunk:0"],
            },
        };
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, new TrackingDimensionValidator(), vectorStore);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));

        var result = await pipeline.UpsertAsync(ingestionResult, "documents-index");

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(["document-1:chunk:0"], result.VectorIds);
    }

    [Fact]
    public async Task UpsertAsync_ShouldReflectVectorStoreFailureResult()
    {
        var mapper = CreateMapper();
        var vectorStore = new TrackingVectorStore
        {
            ForcedResult = new UpsertVectorResult
            {
                Succeeded = false,
                Reason = "Vector index has not been created.",
                IndexName = "documents-index",
            },
        };
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, new TrackingDimensionValidator(), vectorStore);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));

        var result = await pipeline.UpsertAsync(ingestionResult, "documents-index");

        Assert.False(result.Succeeded);
        Assert.Equal("Vector index has not been created.", result.Reason);
        Assert.Equal("documents-index", result.IndexName);
    }

    [Fact]
    public async Task UpsertAsync_ShouldPassCancellationTokenToDimensionValidatorAndVectorStore()
    {
        var mapper = CreateMapper();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new TrackingVectorStore();
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, validator, vectorStore);
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));
        using var cancellationTokenSource = new CancellationTokenSource();

        await pipeline.UpsertAsync(
            ingestionResult,
            "documents-index",
            expectedDimensions: 1,
            cancellationToken: cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, validator.LastCancellationToken);
        Assert.Equal(cancellationTokenSource.Token, vectorStore.LastCancellationToken);
    }

    [Fact]
    public async Task UpsertAsync_ShouldThrowBeforeCallingMapper_WhenCancellationIsAlreadyRequested()
    {
        var mapper = CreateMapper();
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(mapper, new TrackingDimensionValidator(), new TrackingVectorStore());
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.UpsertAsync(ingestionResult, "documents-index", cancellationToken: cancellationTokenSource.Token));

        Assert.False(mapper.WasCalled);
    }

    [Fact]
    public async Task UpsertAsync_ShouldThrow_WhenIngestionResultIsNull()
    {
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(CreateMapper(), new TrackingDimensionValidator(), new TrackingVectorStore());

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            pipeline.UpsertAsync((RagDocumentIngestionResult)null!, "documents-index"));

        Assert.Equal("ingestionResult", exception.ParamName);
    }

    [Fact]
    public async Task UpsertAsync_ShouldThrow_WhenPreparedRequestIsNull()
    {
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(CreateMapper(), new TrackingDimensionValidator(), new TrackingVectorStore());

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            pipeline.UpsertAsync((UpsertVectorRequest)null!));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task UpsertAsync_ShouldStartUpsertFromPreparedVectorRecords()
    {
        var vectorStore = new TrackingVectorStore();
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(CreateMapper(), new TrackingDimensionValidator(), vectorStore);
        var request = new UpsertVectorRequest
        {
            IndexName = "documents-index",
            Records =
            [
                new VectorRecord { Id = "vector-1", Values = [0.1f, 0.2f] },
                new VectorRecord { Id = "vector-2", Values = [0.3f, 0.4f] },
            ],
        };

        var result = await pipeline.UpsertAsync(request);

        Assert.True(vectorStore.UpsertWasCalled);
        Assert.Same(request, vectorStore.LastRequest);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UpsertAsync_ShouldForwardEmptyVectorRecordsToVectorStore()
    {
        var vectorStore = new TrackingVectorStore
        {
            ForcedResult = new UpsertVectorResult
            {
                Succeeded = false,
                Reason = "At least one vector record is required.",
            },
        };
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(CreateMapper(), new TrackingDimensionValidator(), vectorStore);
        var request = new UpsertVectorRequest
        {
            IndexName = "documents-index",
            Records = [],
        };

        var result = await pipeline.UpsertAsync(request);

        Assert.True(vectorStore.UpsertWasCalled);
        Assert.Empty(vectorStore.LastRequest?.Records ?? []);
        Assert.False(result.Succeeded);
        Assert.Equal("At least one vector record is required.", result.Reason);
    }

    [Fact]
    public async Task UpsertAsync_ShouldReturnEmptyResultForIngestionOutputWithNoItems()
    {
        var vectorStore = new TrackingVectorStore
        {
            ForcedResult = new UpsertVectorResult
            {
                Succeeded = true,
                UpsertedCount = 0,
            },
        };
        var pipeline = new DefaultRagVectorStoreUpsertPipeline(CreateMapper(), new TrackingDimensionValidator(), vectorStore);
        var ingestionResult = new RagDocumentIngestionResult
        {
            DocumentId = "document-1",
            Chunks = [],
            Items = [],
        };

        var result = await pipeline.UpsertAsync(ingestionResult, "documents-index");

        Assert.True(vectorStore.UpsertWasCalled);
        Assert.Empty(vectorStore.LastRequest?.Records ?? []);
        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ProcessedCount);
    }

    private static TrackingUpsertVectorRequestMapper CreateMapper()
    {
        return new TrackingUpsertVectorRequestMapper();
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

    private sealed class TrackingUpsertVectorRequestMapper : IRagUpsertVectorRequestMapper
    {
        private readonly IRagUpsertVectorRequestMapper innerMapper =
            new DefaultRagUpsertVectorRequestMapper(new DefaultRagVectorRecordMapper());

        public bool WasCalled { get; private set; }

        public RagDocumentIngestionResult? LastIngestionResult { get; private set; }

        public string LastIndexName { get; private set; } = string.Empty;

        public RagDocumentMetadata? LastDocumentMetadata { get; private set; }

        public UpsertVectorRequest Map(
            RagDocumentIngestionResult ingestionResult,
            string indexName,
            RagDocumentMetadata? documentMetadata = null)
        {
            WasCalled = true;
            LastIngestionResult = ingestionResult;
            LastIndexName = indexName;
            LastDocumentMetadata = documentMetadata;

            return innerMapper.Map(ingestionResult, indexName, documentMetadata);
        }
    }

    private sealed class TrackingDimensionValidator : IRagVectorRecordDimensionValidator
    {
        private readonly DefaultRagVectorRecordDimensionValidator innerValidator = new();

        public bool WasCalled { get; private set; }

        public UpsertVectorRequest? LastRequest { get; private set; }

        public int LastExpectedDimensions { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public VectorRecordDimensionValidationResult? ForcedResult { get; set; }

        public VectorRecordDimensionValidationResult Validate(
            UpsertVectorRequest request,
            int expectedDimensions)
        {
            WasCalled = true;
            LastRequest = request;
            LastExpectedDimensions = expectedDimensions;

            return ForcedResult ?? innerValidator.Validate(request, expectedDimensions);
        }

        public ValueTask<VectorRecordDimensionValidationResult> ValidateAsync(
            UpsertVectorRequest request,
            int expectedDimensions,
            CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;

            return ValueTask.FromResult(Validate(request, expectedDimensions));
        }
    }

    private sealed class TrackingVectorStore : IRagVectorStore
    {
        public bool UpsertWasCalled { get; private set; }

        public UpsertVectorRequest? LastRequest { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public UpsertVectorResult? ForcedResult { get; set; }

        public Task<CreateVectorIndexResult> CreateIndexAsync(
            CreateVectorIndexRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vector store index creation should not be called by the upsert pipeline.");
        }

        public Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            UpsertWasCalled = true;
            LastRequest = request;
            LastCancellationToken = cancellationToken;

            return Task.FromResult(ForcedResult ?? new UpsertVectorResult
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
}
