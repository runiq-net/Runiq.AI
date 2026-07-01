using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.VectorStores;

namespace Runiq.Rag.Tests.VectorStores;

public sealed class ValidatingRagVectorStoreTests
{
    [Fact]
    public async Task UpsertAsync_ShouldValidateAndForwardToInnerStore_WhenRequestIsValid()
    {
        var innerStore = new TrackingVectorStore();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new ValidatingRagVectorStore(innerStore, validator);

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });
        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("vector-1", [0.1f, 0.2f, 0.3f])],
        });

        Assert.True(result.Succeeded);
        Assert.True(validator.WasCalled);
        Assert.True(innerStore.UpsertWasCalled);
        Assert.Equal("documents", validator.IndexName);
        Assert.Equal(3, validator.ExpectedDimensions);
    }

    [Fact]
    public async Task UpsertAsync_ShouldValidateUsingRequestExpectedDimensions_WhenIndexWasNotCreated()
    {
        var innerStore = new TrackingVectorStore();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new ValidatingRagVectorStore(innerStore, validator);

        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            ExpectedDimensions = 3,
            Records = [CreateRecord("vector-1", [0.1f, 0.2f, 0.3f])],
        });

        Assert.True(result.Succeeded);
        Assert.True(validator.WasCalled);
        Assert.True(innerStore.UpsertWasCalled);
        Assert.Equal(3, validator.ExpectedDimensions);
    }

    [Fact]
    public async Task UpsertAsync_ShouldNotForwardToInnerStore_WhenValidationFails()
    {
        var innerStore = new TrackingVectorStore();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new ValidatingRagVectorStore(innerStore, validator);

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });
        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("vector-1", [0.1f, 0.2f])],
        });

        Assert.False(result.Succeeded);
        Assert.True(validator.WasCalled);
        Assert.False(innerStore.UpsertWasCalled);
    }

    [Fact]
    public async Task UpsertAsync_ShouldNotForwardToInnerStore_WhenRequestExpectedDimensionsFail()
    {
        var innerStore = new TrackingVectorStore();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new ValidatingRagVectorStore(innerStore, validator);

        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            ExpectedDimensions = 3,
            Records = [CreateRecord("vector-1", [0.1f, 0.2f])],
        });

        Assert.False(result.Succeeded);
        Assert.True(validator.WasCalled);
        Assert.False(innerStore.UpsertWasCalled);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(2, result.ActualDimensions);
    }

    [Fact]
    public async Task UpsertAsync_ShouldFailDeterministically_WhenExpectedDimensionsAreMissing()
    {
        var innerStore = new TrackingVectorStore();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new ValidatingRagVectorStore(innerStore, validator);

        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("vector-1", [0.1f, 0.2f])],
        });

        Assert.False(result.Succeeded);
        Assert.False(validator.WasCalled);
        Assert.False(innerStore.UpsertWasCalled);
        Assert.Equal("Vector expected dimensions are required for upsert validation.", result.Reason);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
        Assert.Equal(2, result.ActualDimensions);
    }

    [Fact]
    public async Task UpsertAsync_ShouldFailDeterministically_WhenRequestExpectedDimensionsConflictWithCachedIndex()
    {
        var innerStore = new TrackingVectorStore();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new ValidatingRagVectorStore(innerStore, validator);

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 4,
        });
        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            ExpectedDimensions = 3,
            Records = [CreateRecord("vector-1", [0.1f, 0.2f, 0.3f])],
        });

        Assert.False(result.Succeeded);
        Assert.False(validator.WasCalled);
        Assert.False(innerStore.UpsertWasCalled);
        Assert.Equal("Vector expected dimensions conflict with the cached index dimensions.", result.Reason);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(3, result.ActualDimensions);
    }

    [Fact]
    public async Task UpsertAsync_ShouldRejectHigherVectorLength_WhenRequestExpectedDimensionsAreProvided()
    {
        var innerStore = new TrackingVectorStore();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new ValidatingRagVectorStore(innerStore, validator);

        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            ExpectedDimensions = 3,
            Records = [CreateRecord("vector-1", [0.1f, 0.2f, 0.3f, 0.4f])],
        });

        Assert.False(result.Succeeded);
        Assert.True(validator.WasCalled);
        Assert.False(innerStore.UpsertWasCalled);
        Assert.Equal(4, result.ActualDimensions);
    }

    [Fact]
    public async Task UpsertAsync_ShouldPreserveValidationDiagnostics_WhenValidationFails()
    {
        var innerStore = new TrackingVectorStore();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new ValidatingRagVectorStore(innerStore, validator);

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });
        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("vector-1", [0.1f, 0.2f])],
        });

        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(2, result.ActualDimensions);
    }

    [Fact]
    public async Task UpsertAsync_ShouldPassCancellationTokenToValidatorAndInnerStore()
    {
        var innerStore = new TrackingVectorStore();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new ValidatingRagVectorStore(innerStore, validator);
        using var cancellationTokenSource = new CancellationTokenSource();

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });
        await vectorStore.UpsertAsync(
            new UpsertVectorRequest
            {
                IndexName = "documents",
                Records = [CreateRecord("vector-1", [0.1f, 0.2f, 0.3f])],
            },
            cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, validator.LastCancellationToken);
        Assert.Equal(cancellationTokenSource.Token, innerStore.LastUpsertCancellationToken);
    }

    [Fact]
    public async Task ChunkUpsertAsync_ShouldValidateBeforeForwardingToInnerStore()
    {
        var innerStore = new TrackingVectorStore();
        var validator = new TrackingDimensionValidator();
        var vectorStore = new ValidatingRagVectorStore(innerStore, validator);

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });
        var result = await vectorStore.UpsertAsync(
            "documents",
            new RagChunk
            {
                Id = "chunk-1",
                DocumentId = "document-1",
                Content = "content",
            },
            new RagEmbedding([0.1f, 0.2f]));

        Assert.False(result.Succeeded);
        Assert.True(validator.WasCalled);
        Assert.False(innerStore.UpsertWasCalled);
        Assert.Equal("chunk-1", result.RecordId);
    }

    private static VectorRecord CreateRecord(string id, IReadOnlyList<float> values)
    {
        return new VectorRecord
        {
            Id = id,
            Values = values,
        };
    }

    private sealed class TrackingDimensionValidator : IRagVectorRecordDimensionValidator
    {
        private readonly DefaultRagVectorRecordDimensionValidator innerValidator = new();

        public bool WasCalled { get; private set; }

        public string IndexName { get; private set; } = string.Empty;

        public int ExpectedDimensions { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public VectorRecordDimensionValidationResult Validate(
            UpsertVectorRequest request,
            int expectedDimensions)
        {
            WasCalled = true;
            IndexName = request.IndexName;
            ExpectedDimensions = expectedDimensions;

            return innerValidator.Validate(request, expectedDimensions);
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

        public CancellationToken LastUpsertCancellationToken { get; private set; }

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
            UpsertWasCalled = true;
            LastUpsertCancellationToken = cancellationToken;

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
            return Task.FromResult<IReadOnlyList<RagSearchResult>>(Array.Empty<RagSearchResult>());
        }
    }
}
