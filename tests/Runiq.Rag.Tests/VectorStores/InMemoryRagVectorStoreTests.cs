using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.Retrieval;
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
    public async Task CreateIndexAsync_ShouldCreateMultipleIndexes()
    {
        var vectorStore = new InMemoryRagVectorStore();

        var documentsResult = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });
        var archiveResult = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "archive",
            Dimensions = 3,
        });

        Assert.True(documentsResult.Succeeded);
        Assert.True(archiveResult.Succeeded);
        Assert.Equal("documents", documentsResult.IndexName);
        Assert.Equal("archive", archiveResult.IndexName);
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
    public async Task Operations_ShouldIsolateRecordsByIndexName()
    {
        var vectorStore = new InMemoryRagVectorStore();
        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });
        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "archive",
            Dimensions = 3,
        });

        await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("shared-vector", [1.0f, 0.0f, 0.0f], content: "documents record")],
        });
        await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "archive",
            Records = [CreateRecord("shared-vector", [0.0f, 1.0f, 0.0f], content: "archive record")],
        });

        var documentsQuery = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 1,
        });
        var archiveQuery = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "archive",
            Values = [0.0f, 1.0f, 0.0f],
            TopK = 1,
        });

        Assert.Equal("documents record", Assert.Single(documentsQuery.Records).Content);
        Assert.Equal("archive record", Assert.Single(archiveQuery.Records).Content);

        var deleteResult = await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = ["shared-vector"],
        });
        var documentsAfterDelete = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 1,
        });
        var archiveAfterDelete = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "archive",
            Values = [0.0f, 1.0f, 0.0f],
            TopK = 1,
        });

        Assert.True(deleteResult.Succeeded);
        Assert.Empty(documentsAfterDelete.Records);
        Assert.Equal("archive record", Assert.Single(archiveAfterDelete.Records).Content);
    }

    [Fact]
    public async Task UpsertAsync_ShouldAllowSameVectorIdInDifferentIndexes()
    {
        var vectorStore = await CreateVectorStoreWithDocumentsAndArchiveIndexesAsync();

        var documentsResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("shared-vector", [1.0f, 0.0f, 0.0f], content: "documents record")],
        });
        var archiveResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "archive",
            Records = [CreateRecord("shared-vector", [0.0f, 1.0f, 0.0f], content: "archive record")],
        });

        Assert.True(documentsResult.Succeeded);
        Assert.True(archiveResult.Succeeded);
        Assert.Equal("shared-vector", Assert.Single(documentsResult.VectorIds));
        Assert.Equal("shared-vector", Assert.Single(archiveResult.VectorIds));
    }

    [Fact]
    public async Task ChunkUpsertAsync_ShouldStoreChunkInRequestedIndex()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var result = await vectorStore.UpsertAsync(
            "documents",
            CreateChunk("chunk-1", "document chunk"),
            new RagEmbedding([1.0f, 0.0f, 0.0f]));
        var query = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 1,
        });

        Assert.True(result.Succeeded);
        var record = Assert.Single(query.Records);
        Assert.Equal("chunk-1", record.Id);
        Assert.Equal("document chunk", record.Content);
    }

    [Fact]
    public async Task ChunkUpsertAsync_ShouldIsolateRequestedIndexes()
    {
        var vectorStore = await CreateVectorStoreWithDocumentsAndArchiveIndexesAsync();

        await vectorStore.UpsertAsync(
            "documents",
            CreateChunk("document-chunk", "documents content"),
            new RagEmbedding([1.0f, 0.0f, 0.0f]));
        await vectorStore.UpsertAsync(
            "archive",
            CreateChunk("archive-chunk", "archive content"),
            new RagEmbedding([1.0f, 0.0f, 0.0f]));

        var documentsQuery = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 10,
        });
        var archiveQuery = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "archive",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 10,
        });

        Assert.Equal("document-chunk", Assert.Single(documentsQuery.Records).Id);
        Assert.Equal("archive-chunk", Assert.Single(archiveQuery.Records).Id);
    }

    [Fact]
    public async Task ChunkUpsertAsync_ShouldAllowSameVectorIdInDifferentIndexes()
    {
        var vectorStore = await CreateVectorStoreWithDocumentsAndArchiveIndexesAsync();

        var documentsResult = await vectorStore.UpsertAsync(
            "documents",
            CreateChunk("shared-chunk", "documents content"),
            new RagEmbedding([1.0f, 0.0f, 0.0f]));
        var archiveResult = await vectorStore.UpsertAsync(
            "archive",
            CreateChunk("shared-chunk", "archive content"),
            new RagEmbedding([0.0f, 1.0f, 0.0f]));

        Assert.True(documentsResult.Succeeded);
        Assert.True(archiveResult.Succeeded);
        Assert.Equal("shared-chunk", Assert.Single(documentsResult.VectorIds));
        Assert.Equal("shared-chunk", Assert.Single(archiveResult.VectorIds));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ChunkUpsertAsync_ShouldFailDeterministically_WhenIndexNameIsInvalid(string? indexName)
    {
        var vectorStore = await CreateVectorStoreAsync();

        var result = await vectorStore.UpsertAsync(
            indexName!,
            CreateChunk("chunk-1", "document chunk"),
            new RagEmbedding([1.0f, 0.0f, 0.0f]));

        Assert.False(result.Succeeded);
        Assert.Equal("Vector index name is required.", result.Reason);
    }

    [Fact]
    public async Task ChunkUpsertAsync_ShouldFailDeterministically_WhenIndexDoesNotExist()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var result = await vectorStore.UpsertAsync(
            "missing",
            CreateChunk("chunk-1", "document chunk"),
            new RagEmbedding([1.0f, 0.0f, 0.0f]));

        Assert.False(result.Succeeded);
        Assert.Equal("Vector index has not been created.", result.Reason);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnOnlyResultsFromRequestedIndex_AfterChunkUpsert()
    {
        var vectorStore = await CreateVectorStoreWithDocumentsAndArchiveIndexesAsync();
        await vectorStore.UpsertAsync(
            "documents",
            CreateChunk("shared-chunk", "documents content"),
            new RagEmbedding([1.0f, 0.0f, 0.0f]));
        await vectorStore.UpsertAsync(
            "archive",
            CreateChunk("shared-chunk", "archive content"),
            new RagEmbedding([0.0f, 1.0f, 0.0f]));

        var results = await vectorStore.SearchAsync(
            new RagQuery { Text = "query", IndexName = "documents", TopK = 10 },
            new RagEmbedding([1.0f, 0.0f, 0.0f]));

        var result = Assert.Single(results);
        Assert.Equal("shared-chunk", result.Chunk.Id);
        Assert.Equal("documents content", result.Chunk.Content);
    }

    [Fact]
    public async Task SearchAsync_ShouldThrow_WhenIndexDoesNotExist()
    {
        var vectorStore = new InMemoryRagVectorStore();

        var exception = await Assert.ThrowsAsync<RagVectorStoreQueryException>(() =>
            vectorStore.SearchAsync(
                new RagQuery { Text = "query", IndexName = "missing" },
                new RagEmbedding([1.0f, 0.0f, 0.0f])));

        Assert.Equal("Vector index has not been created.", exception.Reason);
        Assert.Equal("missing", exception.IndexName);
    }

    [Fact]
    public async Task SearchAsync_ShouldThrow_WhenDimensionDoesNotMatch()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var exception = await Assert.ThrowsAsync<RagVectorStoreQueryException>(() =>
            vectorStore.SearchAsync(
                new RagQuery { Text = "query", IndexName = "documents" },
                new RagEmbedding([1.0f, 0.0f])));

        Assert.Equal("Vector dimension does not match the index dimensions.", exception.Reason);
    }

    [Fact]
    public async Task SearchAsync_ShouldThrow_WhenTopKIsInvalid()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var exception = await Assert.ThrowsAsync<RagVectorStoreQueryException>(() =>
            vectorStore.SearchAsync(
                new RagQuery { Text = "query", IndexName = "documents", TopK = 0 },
                new RagEmbedding([1.0f, 0.0f, 0.0f])));

        Assert.Equal("TopK must be greater than zero.", exception.Reason);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnEmptyResults_WhenQuerySucceedsButNoRecordsMatch()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var results = await vectorStore.SearchAsync(
            new RagQuery { Text = "query", IndexName = "documents" },
            new RagEmbedding([1.0f, 0.0f, 0.0f]));

        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryAsync_ShouldNotMixRecordsBetweenIndexes()
    {
        var vectorStore = await CreateVectorStoreWithDocumentsAndArchiveIndexesAsync();
        await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("document-vector", [1.0f, 0.0f, 0.0f], content: "documents record")],
        });
        await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "archive",
            Records = [CreateRecord("archive-vector", [1.0f, 0.0f, 0.0f], content: "archive record")],
        });

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 10,
        });

        Assert.True(result.Succeeded);
        Assert.Equal("document-vector", Assert.Single(result.Records).Id);
    }

    [Fact]
    public async Task DeleteAsync_ShouldOnlyDeleteFromRequestedIndex_WhenSameVectorIdExistsInMultipleIndexes()
    {
        var vectorStore = await CreateVectorStoreWithDocumentsAndArchiveIndexesAsync();
        await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("shared-vector", [1.0f, 0.0f, 0.0f], content: "documents record")],
        });
        await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "archive",
            Records = [CreateRecord("shared-vector", [1.0f, 0.0f, 0.0f], content: "archive record")],
        });

        var deleteResult = await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = ["shared-vector"],
        });
        var archiveQuery = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "archive",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 1,
        });

        Assert.True(deleteResult.Succeeded);
        Assert.Equal(1, deleteResult.DeletedCount);
        Assert.Equal("archive record", Assert.Single(archiveQuery.Records).Content);
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
    public async Task QueryAsync_ShouldExposeHigherScoreForBetterMatch()
    {
        var vectorStore = await CreateVectorStoreWithThreeRecordsAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
        });

        Assert.True(result.Records[0].Score > result.Records[1].Score);
        Assert.True(result.Records[1].Score > result.Records[2].Score);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnMatchingRecords_WhenMetadataFilterMatches()
    {
        var vectorStore = await CreateVectorStoreWithMetadataAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
            MetadataFilter = CreateMetadata(("tenant", "runiq")),
        });

        Assert.True(result.Succeeded);
        Assert.Equal(["vector-a", "vector-c"], result.Records.Select(record => record.Id));
    }

    [Fact]
    public async Task QueryAsync_ShouldNotReturnRecords_WhenMetadataFilterDoesNotMatch()
    {
        var vectorStore = await CreateVectorStoreWithMetadataAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
            MetadataFilter = CreateMetadata(("tenant", "missing")),
        });

        Assert.True(result.Succeeded);
        Assert.Empty(result.Records);
    }

    [Fact]
    public async Task QueryAsync_ShouldApplyMetadataFilterWithAndSemantics()
    {
        var vectorStore = await CreateVectorStoreWithMetadataAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
            MetadataFilter = CreateMetadata(("tenant", "runiq"), ("source", "docs")),
        });

        Assert.True(result.Succeeded);
        Assert.Equal("vector-a", Assert.Single(result.Records).Id);
    }

    [Fact]
    public async Task QueryAsync_ShouldPreserveExistingBehavior_WhenMetadataFilterIsEmpty()
    {
        var vectorStore = await CreateVectorStoreWithMetadataAsync();

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Operations_ShouldFailDeterministically_WhenIndexNameIsInvalid(string? indexName)
    {
        var vectorStore = new InMemoryRagVectorStore();

        var createResult = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = indexName!,
            Dimensions = 3,
        });
        var upsertResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = indexName!,
            Records = [CreateRecord("vector-1", [1.0f, 0.0f, 0.0f])],
        });
        var queryResult = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = indexName!,
            Values = [1.0f, 0.0f, 0.0f],
        });
        var deleteResult = await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = indexName!,
            VectorIds = ["vector-1"],
        });

        Assert.False(createResult.Succeeded);
        Assert.False(upsertResult.Succeeded);
        Assert.False(queryResult.Succeeded);
        Assert.False(deleteResult.Succeeded);
        Assert.Equal("Vector index name is required.", createResult.Reason);
        Assert.Equal(createResult.Reason, upsertResult.Reason);
        Assert.Equal(createResult.Reason, queryResult.Reason);
        Assert.Equal(createResult.Reason, deleteResult.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateIndexAsync_ShouldFailDeterministically_WhenDimensionsAreInvalid(int dimensions)
    {
        var vectorStore = new InMemoryRagVectorStore();

        var result = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = dimensions,
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Vector dimensions must be greater than zero.", result.Reason);
    }

    [Fact]
    public async Task UpsertAsync_ShouldFailDeterministically_WhenRequestIsNull()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var result = await vectorStore.UpsertAsync(null!);

        Assert.False(result.Succeeded);
        Assert.Equal("Request is required.", result.Reason);
    }

    [Fact]
    public async Task UpsertAsync_ShouldFailDeterministically_WhenRecordsAreNullOrEmpty()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var nullRecordsResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = null!,
        });
        var emptyRecordsResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [],
        });

        Assert.False(nullRecordsResult.Succeeded);
        Assert.False(emptyRecordsResult.Succeeded);
        Assert.Equal("At least one vector record is required.", nullRecordsResult.Reason);
        Assert.Equal(nullRecordsResult.Reason, emptyRecordsResult.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpsertAsync_ShouldFailDeterministically_WhenVectorIdIsInvalid(string? vectorId)
    {
        var vectorStore = await CreateVectorStoreAsync();

        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord(vectorId!, [1.0f, 0.0f, 0.0f])],
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Vector identifier is required.", result.Reason);
    }

    [Fact]
    public async Task UpsertAsync_ShouldFailDeterministically_WhenVectorValuesAreNullOrEmpty()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var nullValuesResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("vector-1", null!)],
        });
        var emptyValuesResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("vector-1", [])],
        });

        Assert.False(nullValuesResult.Succeeded);
        Assert.False(emptyValuesResult.Succeeded);
        Assert.Equal("Vector values are required.", nullValuesResult.Reason);
        Assert.Equal(nullValuesResult.Reason, emptyValuesResult.Reason);
    }

    [Fact]
    public async Task QueryAsync_ShouldFailDeterministically_WhenVectorValuesAreNullOrEmpty()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var nullValuesResult = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = null!,
        });
        var emptyValuesResult = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [],
        });

        Assert.False(nullValuesResult.Succeeded);
        Assert.False(emptyValuesResult.Succeeded);
        Assert.Equal("Vector values are required.", nullValuesResult.Reason);
        Assert.Equal(nullValuesResult.Reason, emptyValuesResult.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task QueryAsync_ShouldFailDeterministically_WhenTopKIsInvalid(int topK)
    {
        var vectorStore = await CreateVectorStoreAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = topK,
        });

        Assert.False(result.Succeeded);
        Assert.Empty(result.Records);
        Assert.Equal("TopK must be greater than zero.", result.Reason);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnSuccessfulNoOp_WhenVectorIdsAreNullOrEmpty()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var nullIdsResult = await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = null!,
        });
        var emptyIdsResult = await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = [],
        });

        Assert.True(nullIdsResult.Succeeded);
        Assert.True(emptyIdsResult.Succeeded);
        Assert.Equal(0, nullIdsResult.RequestedCount);
        Assert.Equal(0, emptyIdsResult.RequestedCount);
        Assert.Equal(0, nullIdsResult.DeletedCount);
        Assert.Equal(0, emptyIdsResult.DeletedCount);
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

    private static async Task<InMemoryRagVectorStore> CreateVectorStoreWithDocumentsAndArchiveIndexesAsync()
    {
        var vectorStore = await CreateVectorStoreAsync();

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "archive",
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

    private static async Task<InMemoryRagVectorStore> CreateVectorStoreWithMetadataAsync()
    {
        var vectorStore = await CreateVectorStoreAsync();

        await UpsertRecordsAsync(
            vectorStore,
            CreateRecord("vector-a", [1.0f, 0.0f, 0.0f], metadata: CreateMetadata(("tenant", "runiq"), ("source", "docs"))),
            CreateRecord("vector-b", [0.0f, 1.0f, 0.0f], metadata: CreateMetadata(("tenant", "other"), ("source", "docs"))),
            CreateRecord("vector-c", [0.5f, 0.5f, 0.0f], metadata: CreateMetadata(("tenant", "runiq"), ("source", "blog"))));

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

    private static RagChunk CreateChunk(string id, string content)
    {
        return new RagChunk
        {
            Id = id,
            DocumentId = "document-1",
            Content = content,
        };
    }

    private static RagMetadata CreateMetadata(params (string Key, string Value)[] values)
    {
        return new RagMetadata(values.ToDictionary(value => value.Key, value => value.Value));
    }
}
