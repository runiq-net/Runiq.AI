using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.VectorStores.InMemory;

namespace Runiq.Rag.Tests.VectorStores;

public sealed class InMemoryRagVectorStoreTests
{
    [Fact]
    public async Task CreateIndexAsync_ShouldCreateIndex()
    {
        var vectorStore = new InMemoryRagVectorStore();

        var result = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });

        Assert.True(result.Succeeded);
        Assert.Equal("documents", result.IndexName);
    }

    [Fact]
    public async Task UpsertAsync_ShouldStoreSingleVectorRecord()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records =
            [
                CreateRecord("vector-1", [1.0f, 0.0f, 0.0f]),
            ],
        });

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.UpsertedCount);
        Assert.Equal("vector-1", Assert.Single(result.VectorIds));
    }

    [Fact]
    public async Task UpsertAsync_ShouldStoreMultipleVectorRecords()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records =
            [
                CreateRecord("vector-1", [1.0f, 0.0f, 0.0f]),
                CreateRecord("vector-2", [0.0f, 1.0f, 0.0f]),
            ],
        });

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.UpsertedCount);
        Assert.Equal(["vector-1", "vector-2"], result.VectorIds);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpdateRecord_WhenSameVectorIdIsUpsertedAgain()
    {
        var vectorStore = await CreateVectorStoreAsync();
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [1.0f, 0.0f, 0.0f], content: "before"));

        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [0.0f, 1.0f, 0.0f], content: "after"));
        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [0.0f, 1.0f, 0.0f],
            TopK = 1,
        });

        var record = Assert.Single(result.Records);
        Assert.Equal("vector-1", record.Id);
        Assert.Equal("after", record.Content);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnMostSimilarRecordsFirst()
    {
        var vectorStore = await CreateVectorStoreWithThreeRecordsAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
        });

        Assert.True(result.Succeeded);
        Assert.Equal(["vector-a", "vector-c", "vector-b"], result.Records.Select(record => record.Id));
    }

    [Fact]
    public async Task QueryAsync_ShouldLimitResultsByTopK()
    {
        var vectorStore = await CreateVectorStoreWithThreeRecordsAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 2,
        });

        Assert.Equal(2, result.Records.Count);
        Assert.Equal(["vector-a", "vector-c"], result.Records.Select(record => record.Id));
    }

    [Fact]
    public async Task QueryAsync_ShouldIncludeVectorId()
    {
        var vectorStore = await CreateVectorStoreAsync();
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [1.0f, 0.0f, 0.0f]));

        var result = await QuerySingleAsync(vectorStore);

        Assert.Equal("vector-1", result.Id);
    }

    [Fact]
    public async Task QueryAsync_ShouldIncludeScore()
    {
        var vectorStore = await CreateVectorStoreAsync();
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [1.0f, 0.0f, 0.0f]));

        var result = await QuerySingleAsync(vectorStore);

        Assert.Equal(1.0d, result.Score, precision: 6);
    }

    [Fact]
    public async Task QueryAsync_ShouldIncludeVectorValues_WhenRequested()
    {
        var vectorStore = await CreateVectorStoreAsync();
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [1.0f, 0.0f, 0.0f]));

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 1,
            IncludeVectors = true,
        });

        Assert.Equal([1.0f, 0.0f, 0.0f], Assert.Single(result.Records).Values);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnStoredMetadata()
    {
        var vectorStore = await CreateVectorStoreAsync();
        await UpsertRecordsAsync(
            vectorStore,
            CreateRecord(
                "vector-1",
                [1.0f, 0.0f, 0.0f],
                metadata: new RagMetadata(new Dictionary<string, string>
                {
                    ["source"] = "unit-test",
                })));

        var result = await QuerySingleAsync(vectorStore);

        Assert.Equal("unit-test", result.Metadata.Values["source"]);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteSingleVectorId()
    {
        var vectorStore = await CreateVectorStoreWithThreeRecordsAsync();

        var result = await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = ["vector-a"],
        });

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal("vector-a", Assert.Single(result.VectorIds));
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteMultipleVectorIds()
    {
        var vectorStore = await CreateVectorStoreWithThreeRecordsAsync();

        var result = await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = ["vector-a", "vector-b"],
        });

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(["vector-a", "vector-b"], result.VectorIds);
    }

    [Fact]
    public async Task QueryAsync_ShouldNotReturnDeletedVector()
    {
        var vectorStore = await CreateVectorStoreWithThreeRecordsAsync();
        await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = ["vector-a"],
        });

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
        });

        Assert.DoesNotContain(result.Records, record => record.Id == "vector-a");
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnDeterministicNotFoundResult_WhenVectorIdDoesNotExist()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var result = await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = ["missing"],
        });

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.RequestedCount);
        Assert.Equal(0, result.DeletedCount);
        Assert.Empty(result.VectorIds);
        Assert.Equal("missing", Assert.Single(result.NotFoundVectorIds));
    }

    [Fact]
    public async Task Operations_ShouldFailDeterministically_WhenIndexIsNotCreated()
    {
        var vectorStore = new InMemoryRagVectorStore();

        var upsertResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "missing-index",
            Records = [CreateRecord("vector-1", [1.0f, 0.0f, 0.0f])],
        });
        var queryResult = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "missing-index",
            Values = [1.0f, 0.0f, 0.0f],
        });
        var deleteResult = await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = "missing-index",
            VectorIds = ["vector-1"],
        });

        Assert.False(upsertResult.Succeeded);
        Assert.False(queryResult.Succeeded);
        Assert.False(deleteResult.Succeeded);
        Assert.Equal("Vector index has not been created.", upsertResult.Reason);
        Assert.Equal(upsertResult.Reason, queryResult.Reason);
        Assert.Equal(upsertResult.Reason, deleteResult.Reason);
    }

    [Fact]
    public async Task Operations_ShouldFailDeterministically_WhenDimensionDoesNotMatch()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var upsertResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("vector-1", [1.0f, 0.0f])],
        });
        var queryResult = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f],
        });

        Assert.False(upsertResult.Succeeded);
        Assert.False(queryResult.Succeeded);
        Assert.Equal("Vector dimension does not match the index dimensions.", upsertResult.Reason);
        Assert.Equal(upsertResult.Reason, queryResult.Reason);
    }

    private static async Task<InMemoryRagVectorStore> CreateVectorStoreAsync()
    {
        var vectorStore = new InMemoryRagVectorStore();

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });

        return vectorStore;
    }

    private static async Task<InMemoryRagVectorStore> CreateVectorStoreWithThreeRecordsAsync()
    {
        var vectorStore = await CreateVectorStoreAsync();

        await UpsertRecordsAsync(
            vectorStore,
            CreateRecord("vector-a", [1.0f, 0.0f, 0.0f]),
            CreateRecord("vector-b", [0.0f, 1.0f, 0.0f]),
            CreateRecord("vector-c", [0.5f, 0.5f, 0.0f]));

        return vectorStore;
    }

    private static async Task UpsertRecordsAsync(
        InMemoryRagVectorStore vectorStore,
        params VectorRecord[] records)
    {
        await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = records,
        });
    }

    private static async Task<VectorSearchResult> QuerySingleAsync(InMemoryRagVectorStore vectorStore)
    {
        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 1,
        });

        return Assert.Single(result.Records);
    }

    private static VectorRecord CreateRecord(
        string id,
        IReadOnlyList<float> values,
        string content = "",
        RagMetadata? metadata = null)
    {
        return new VectorRecord
        {
            Id = id,
            Values = values,
            Content = content,
            Metadata = metadata ?? RagMetadata.Empty,
        };
    }
}
