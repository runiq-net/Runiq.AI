using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.Retrieval;
using Runiq.AI.Rag.VectorStores;
using Runiq.AI.Rag.VectorStores.InMemory;

namespace Runiq.AI.Rag.Tests.VectorStores;

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

    // Verifies that a single record can be written to an index and that the successful result exposes the
    // complete standard upsert result contract (error code, counts, and partial-success flag).
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
        Assert.Equal(VectorStoreUpsertErrorCode.None, result.ErrorCode);
        Assert.Equal(1, result.UpsertedCount);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.AttemptedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(result.SupportsPartialSuccess);
        Assert.Equal(string.Empty, result.Reason);
        Assert.Equal("vector-1", Assert.Single(result.VectorIds));
    }

    // Verifies that multiple records can be written to the same index in one request and that the successful
    // result reports every attempted record as processed.
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
        Assert.Equal(VectorStoreUpsertErrorCode.None, result.ErrorCode);
        Assert.Equal(2, result.UpsertedCount);
        Assert.Equal(2, result.AttemptedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(result.SupportsPartialSuccess);
        Assert.Equal(["vector-1", "vector-2"], result.VectorIds);
    }

    // Verifies that upserting the same record id into the same index again replaces the stored record content.
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

    // Verifies that upserting the same record id twice does not create a duplicate: the total number of records
    // stored in the index stays at one, which a TopK=1 read alone could not prove.
    [Fact]
    public async Task UpsertAsync_ShouldNotIncreaseRecordCount_WhenSameVectorIdIsUpsertedAgain()
    {
        var vectorStore = await CreateVectorStoreAsync();
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [1.0f, 0.0f, 0.0f], content: "before"));

        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [0.0f, 1.0f, 0.0f], content: "after"));
        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [0.0f, 1.0f, 0.0f],
            TopK = 10,
        });

        var record = Assert.Single(result.Records);
        Assert.Equal("vector-1", record.Id);
        Assert.Equal("after", record.Content);
    }

    // Verifies that a request-based upsert with an already-cancelled token throws and does not write any record.
    [Fact]
    public async Task UpsertAsync_ShouldThrowAndWriteNothing_WhenCancellationIsAlreadyRequested()
    {
        var vectorStore = await CreateVectorStoreAsync();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            vectorStore.UpsertAsync(
                new UpsertVectorRequest
                {
                    IndexName = "documents",
                    Records = [CreateRecord("vector-1", [1.0f, 0.0f, 0.0f])],
                },
                cancellationTokenSource.Token));

        var query = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 10,
        });
        Assert.Empty(query.Records);
    }

    // Verifies that a chunk-based upsert with an already-cancelled token throws and does not write any record.
    [Fact]
    public async Task ChunkUpsertAsync_ShouldThrowAndWriteNothing_WhenCancellationIsAlreadyRequested()
    {
        var vectorStore = await CreateVectorStoreAsync();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            vectorStore.UpsertAsync(
                "documents",
                CreateChunk("chunk-1", "document chunk"),
                new RagEmbedding([1.0f, 0.0f, 0.0f]),
                cancellationTokenSource.Token));

        var query = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 10,
        });
        Assert.Empty(query.Records);
    }

    // Verifies that a query with an already-cancelled token fails fast with OperationCanceledException,
    // matching the repo-standard cancellation behavior used by the upsert operations.
    [Fact]
    public async Task QueryAsync_ShouldThrow_WhenCancellationIsAlreadyRequested()
    {
        var vectorStore = await CreateVectorStoreAsync();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            vectorStore.QueryAsync(
                new QueryVectorRequest
                {
                    IndexName = "documents",
                    Values = [1.0f, 0.0f, 0.0f],
                    TopK = 10,
                },
                cancellationTokenSource.Token));
    }

    // Verifies that the request model rejects an invalid index name at construction time, so a request-based
    // upsert can never reach the store with a null, empty, or whitespace index name, and store state is untouched.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpsertAsync_RequestConstruction_ShouldFailFast_WhenIndexNameIsInvalid(string? indexName)
    {
        var vectorStore = await CreateVectorStoreAsync();

        var exception = Assert.Throws<ArgumentException>(() => new UpsertVectorRequest
        {
            IndexName = indexName!,
            Records = [CreateRecord("vector-1", [1.0f, 0.0f, 0.0f])],
        });

        Assert.Equal(nameof(UpsertVectorRequest.IndexName), exception.ParamName);

        var query = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 10,
        });
        Assert.Empty(query.Records);
    }

    // Verifies the all-or-nothing guarantee of the raw in-memory store: an invalid record later in the batch is
    // detected before any write, so earlier valid records are not stored and the whole batch is reported as failed.
    [Fact]
    public async Task UpsertAsync_ShouldWriteNothing_WhenLaterRecordInBatchIsInvalid()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records =
            [
                CreateRecord("valid-vector", [1.0f, 0.0f, 0.0f]),
                CreateRecord("invalid-vector", []),
            ],
        });
        var query = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 10,
        });

        Assert.False(result.Succeeded);
        Assert.Equal(VectorStoreUpsertErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(2, result.AttemptedCount);
        Assert.Equal(2, result.FailedCount);
        Assert.False(result.SupportsPartialSuccess);
        Assert.Empty(result.VectorIds);
        Assert.Empty(query.Records);
    }

    // Verifies that stored metadata is a defensive copy: mutating the source metadata after the upsert does not
    // change the metadata already stored for the record.
    [Fact]
    public async Task UpsertAsync_ShouldDefensivelyCopyMetadata()
    {
        var vectorStore = await CreateVectorStoreAsync();
        var metadata = CreateMetadata(("tenant", "runiq"));
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [1.0f, 0.0f, 0.0f], metadata: metadata));

        metadata.Values["tenant"] = "mutated";
        var record = await QuerySingleAsync(vectorStore);

        Assert.Equal("runiq", record.Metadata.Values["tenant"]);
    }

    // Verifies that stored vector values are a defensive copy: mutating the source values array after the upsert
    // does not change the values already stored for the record.
    [Fact]
    public async Task UpsertAsync_ShouldDefensivelyCopyVectorValues()
    {
        var vectorStore = await CreateVectorStoreAsync();
        var values = new float[] { 1.0f, 0.0f, 0.0f };
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", values));

        values[0] = -1.0f;
        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 1,
            IncludeVectors = true,
        });

        var record = Assert.Single(result.Records);
        Assert.Equal([1.0f, 0.0f, 0.0f], record.Values);
    }

    // Verifies that records written to different indexes are isolated: upsert, query, and delete operations
    // only ever observe records that belong to the requested index name.
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

    // Verifies that the same record id can be stored independently in different indexes without one index's
    // record overwriting or conflicting with the other's.
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

    // Verifies that chunk-based upserts targeting different indexes keep their records isolated per index.
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

    // Verifies that the same chunk id can be upserted into different indexes independently via the chunk-based
    // overload.
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

    // Verifies that a chunk-based upsert with an invalid index name fails deterministically with the standard
    // validation failure contract and does not write anything.
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
        Assert.Equal(VectorStoreUpsertErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(1, result.AttemptedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.False(result.SupportsPartialSuccess);
        Assert.Empty(result.VectorIds);
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

    // Verifies that vector search is isolated to the requested index and never returns records stored under a
    // different index in the same store.
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

    // Verifies that query results are ordered by similarity score with the best match returned first.
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

    // Verifies that a closer vector match is exposed with a strictly higher similarity score than weaker matches.
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

        Assert.True(result.Records[0].RawScore > result.Records[1].RawScore);
        Assert.True(result.Records[1].RawScore > result.Records[2].RawScore);
    }

    // Verifies that a single equality criterion returns only the records whose metadata satisfies the filter.
    [Fact]
    public async Task QueryAsync_ShouldReturnMatchingRecords_WhenMetadataFilterMatches()
    {
        var vectorStore = await CreateVectorStoreWithMetadataAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
            MetadataFilter = CreateFilter(("tenant", "runiq")),
        });

        Assert.True(result.Succeeded);
        Assert.Equal(["vector-a", "vector-c"], result.Records.Select(record => record.Id));
    }

    // Verifies that a metadata filter with no matching records yields a successful but empty result.
    [Fact]
    public async Task QueryAsync_ShouldNotReturnRecords_WhenMetadataFilterDoesNotMatch()
    {
        var vectorStore = await CreateVectorStoreWithMetadataAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
            MetadataFilter = CreateFilter(("tenant", "missing")),
        });

        Assert.True(result.Succeeded);
        Assert.Empty(result.Records);
    }

    // Verifies that multiple equality criteria are combined with AND semantics, so a record must match every
    // criterion to be returned.
    [Fact]
    public async Task QueryAsync_ShouldApplyMetadataFilterWithAndSemantics()
    {
        var vectorStore = await CreateVectorStoreWithMetadataAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
            MetadataFilter = CreateFilter(("tenant", "runiq"), ("source", "docs")),
        });

        Assert.True(result.Succeeded);
        Assert.Equal("vector-a", Assert.Single(result.Records).Id);
    }

    // Verifies that metadata filtering is applied before similarity ordering and TopK selection: the overall
    // best-scoring record is filtered out, so TopK=1 returns the best match of the filtered subset instead of
    // an empty result.
    [Fact]
    public async Task QueryAsync_ShouldApplyMetadataFilterBeforeSimilarityOrderingAndTopK()
    {
        var vectorStore = await CreateVectorStoreWithMetadataAsync();

        // For query [0,1,0] the best overall match is vector-b, but vector-b belongs to tenant "other".
        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [0.0f, 1.0f, 0.0f],
            TopK = 1,
            MetadataFilter = CreateFilter(("tenant", "runiq")),
        });

        Assert.True(result.Succeeded);
        Assert.Equal("vector-c", Assert.Single(result.Records).Id);
    }

    // Verifies that after metadata filtering the surviving records remain ordered by similarity score in
    // descending order.
    [Fact]
    public async Task QueryAsync_ShouldKeepFilteredResultsOrderedByDescendingScore()
    {
        var vectorStore = await CreateVectorStoreWithMetadataAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
            MetadataFilter = CreateFilter(("tenant", "runiq")),
        });

        Assert.Equal(2, result.Records.Count);
        Assert.Equal(["vector-a", "vector-c"], result.Records.Select(record => record.Id));
        Assert.True(result.Records[0].RawScore > result.Records[1].RawScore);
    }

    // Verifies that a record whose metadata does not contain the criterion key never matches the filter.
    [Fact]
    public async Task QueryAsync_ShouldNotMatchRecord_WhenMetadataKeyIsMissingOnRecord()
    {
        var vectorStore = await CreateVectorStoreAsync();
        await UpsertRecordsAsync(
            vectorStore,
            CreateRecord("vector-with-key", [1.0f, 0.0f, 0.0f], metadata: CreateMetadata(("tenant", "runiq"))),
            CreateRecord("vector-without-key", [1.0f, 0.0f, 0.0f], metadata: CreateMetadata(("source", "docs"))));

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
            MetadataFilter = CreateFilter(("tenant", "runiq")),
        });

        Assert.True(result.Succeeded);
        Assert.Equal("vector-with-key", Assert.Single(result.Records).Id);
    }

    // Verifies that a record carrying no metadata at all never matches a non-empty filter.
    [Fact]
    public async Task QueryAsync_ShouldNotMatchRecord_WhenRecordHasNoMetadata()
    {
        var vectorStore = await CreateVectorStoreAsync();
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [1.0f, 0.0f, 0.0f]));

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
            MetadataFilter = CreateFilter(("tenant", "runiq")),
        });

        Assert.True(result.Succeeded);
        Assert.Empty(result.Records);
    }

    // Verifies that a filter carrying an operator the in-memory store does not support fails deterministically
    // with a failure result instead of being silently ignored or partially applied.
    [Fact]
    public async Task QueryAsync_ShouldFailDeterministically_WhenFilterOperatorIsUnsupported()
    {
        var vectorStore = await CreateVectorStoreWithMetadataAsync();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 3,
            MetadataFilter = new RetrievalMetadataFilter(
            [
                new RetrievalMetadataFilterCriterion("tenant", "runiq", (RetrievalMetadataFilterOperator)999),
            ]),
        });

        Assert.False(result.Succeeded);
        Assert.Empty(result.Records);
        Assert.Equal("Metadata filter operator is not supported.", result.Reason);
    }

    // Verifies that metadata filtering stays isolated per index: the same filter applied to different indexes
    // only ever matches records stored under the queried index.
    [Fact]
    public async Task QueryAsync_ShouldNotMixMetadataFilterMatchesBetweenIndexes()
    {
        var vectorStore = await CreateVectorStoreWithDocumentsAndArchiveIndexesAsync();
        await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("documents-vector", [1.0f, 0.0f, 0.0f], metadata: CreateMetadata(("tenant", "runiq")))],
        });
        await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "archive",
            Records = [CreateRecord("archive-vector", [1.0f, 0.0f, 0.0f], metadata: CreateMetadata(("tenant", "runiq")))],
        });

        var documentsResult = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 10,
            MetadataFilter = CreateFilter(("tenant", "runiq")),
        });
        var archiveResult = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "archive",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 10,
            MetadataFilter = CreateFilter(("tenant", "runiq")),
        });

        Assert.Equal("documents-vector", Assert.Single(documentsResult.Records).Id);
        Assert.Equal("archive-vector", Assert.Single(archiveResult.Records).Id);
    }

    // Verifies that an empty metadata filter does not exclude any record, preserving unfiltered query behavior.
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

    // Verifies that TopK limits the number of returned matches to the best-scoring records.
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

    // Verifies that each query result carries the identifier of the matched vector record.
    [Fact]
    public async Task QueryAsync_ShouldIncludeVectorId()
    {
        var vectorStore = await CreateVectorStoreAsync();
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [1.0f, 0.0f, 0.0f]));

        var result = await QuerySingleAsync(vectorStore);

        Assert.Equal("vector-1", result.Id);
    }

    // Verifies that each query result carries the provider raw score without presenting it as confidence.
    [Fact]
    public async Task QueryAsync_ShouldIncludeRawScore()
    {
        var vectorStore = await CreateVectorStoreAsync();
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [1.0f, 0.0f, 0.0f]));

        var result = await QuerySingleAsync(vectorStore);

        Assert.Equal(1.0d, result.RawScore, precision: 6);
    }

    [Fact]
    // Verifies that the in-memory cosine adapter exposes raw metric semantics and the documented [0,1] normalization.
    public async Task QueryAsync_ShouldExposeNormalizedCosineScoreSemantics()
    {
        var vectorStore = await CreateVectorStoreAsync();
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [0.0f, 1.0f, 0.0f]));

        var result = await QuerySingleAsync(vectorStore);

        Assert.Equal(RagScoreMetrics.CosineSimilarity, result.Metric);
        Assert.True(result.HigherIsBetter);
        Assert.Equal(0.0, result.RawScore, precision: 6);
        Assert.Equal(0.5, result.Relevance!.Value, precision: 6);
    }

    [Fact]
    // Verifies that Euclidean distance remains lower-is-better raw data while normalized relevance orders closer matches first.
    public async Task QueryAsync_ShouldExposeLowerIsBetterEuclideanScoreSemantics()
    {
        var vectorStore = new InMemoryRagVectorStore();
        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 2,
            Metric = VectorDistanceMetric.Euclidean,
        });
        await UpsertRecordsAsync(
            vectorStore,
            CreateRecord("near", [0.5f, 0.0f]),
            CreateRecord("far", [0.0f, 1.0f]));

        var query = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f],
            TopK = 2,
        });

        Assert.Equal(["near", "far"], query.Records.Select(item => item.Id));
        Assert.All(query.Records, item => Assert.Equal(RagScoreMetrics.EuclideanDistance, item.Metric));
        Assert.All(query.Records, item => Assert.False(item.HigherIsBetter));
        Assert.True(query.Records[0].RawScore < query.Records[1].RawScore);
        Assert.True(query.Records[0].Relevance > query.Records[1].Relevance);
    }

    [Fact]
    // Verifies that unbounded dot-product scores are labeled explicitly and do not invent common relevance values.
    public async Task QueryAsync_ShouldLeaveDotProductRelevanceUndefined()
    {
        var vectorStore = new InMemoryRagVectorStore();
        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 2,
            Metric = VectorDistanceMetric.DotProduct,
        });
        await UpsertRecordsAsync(vectorStore, CreateRecord("vector-1", [2.0f, 1.0f]));

        var query = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f],
        });
        var result = Assert.Single(query.Records);

        Assert.Equal(RagScoreMetrics.DotProduct, result.Metric);
        Assert.True(result.HigherIsBetter);
        Assert.Equal(2.0, result.RawScore);
        Assert.Null(result.Relevance);
    }

    // Verifies that vector values are preserved with the stored record and can be read back when requested.
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

    // Verifies that metadata is preserved with the stored record and returned unchanged when the record is read
    // back.
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

    // Verifies that a record removed by delete is no longer returned by a subsequent vector search.
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
        Assert.Equal(VectorStoreUpsertErrorCode.StoreFailed, upsertResult.ErrorCode);
        Assert.Equal(0, upsertResult.ProcessedCount);
        Assert.Equal(1, upsertResult.AttemptedCount);
        Assert.Equal(1, upsertResult.FailedCount);
        Assert.False(upsertResult.SupportsPartialSuccess);
        Assert.Empty(upsertResult.VectorIds);
    }

    [Fact]
    public async Task Operations_ShouldFailDeterministically_WhenDimensionDoesNotMatch()
    {
        var vectorStore = new ValidatingRagVectorStore(
            new InMemoryRagVectorStore(),
            new DefaultRagVectorRecordDimensionValidator());

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });

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

    [Fact]
    public async Task ValidatingUpsertAsync_ShouldPreserveDimensionValidationDiagnostics_WhenDimensionDoesNotMatch()
    {
        var vectorStore = new ValidatingRagVectorStore(
            new InMemoryRagVectorStore(),
            new Runiq.AI.Rag.VectorStores.DefaultRagVectorRecordDimensionValidator());
        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });

        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("vector-1", [1.0f, 0.0f])],
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Vector dimension does not match the index dimensions.", result.Reason);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(2, result.ActualDimensions);
    }

    [Fact]
    public async Task ValidatingUpsertAsync_ShouldNotWriteAnyRecord_WhenMultiRecordRequestContainsInvalidDimensions()
    {
        var vectorStore = new ValidatingRagVectorStore(
            new InMemoryRagVectorStore(),
            new Runiq.AI.Rag.VectorStores.DefaultRagVectorRecordDimensionValidator());
        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });

        var upsertResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records =
            [
                CreateRecord("valid-vector", [1.0f, 0.0f, 0.0f]),
                CreateRecord("invalid-vector", [1.0f, 0.0f]),
            ],
        });
        var queryResult = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [1.0f, 0.0f, 0.0f],
            TopK = 10,
        });

        Assert.False(upsertResult.Succeeded);
        Assert.Equal("invalid-vector", upsertResult.RecordId);
        Assert.Empty(queryResult.Records);
    }

    [Fact]
    public async Task ValidatingUpsertAsync_ShouldUseProviderIndependentDimensionValidator()
    {
        var validator = new TrackingDimensionValidator();
        var vectorStore = new ValidatingRagVectorStore(new InMemoryRagVectorStore(), validator);

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });

        await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [CreateRecord("vector-1", [1.0f, 0.0f, 0.0f])],
        });

        Assert.True(validator.WasCalled);
        Assert.Equal("documents", validator.IndexName);
        Assert.Equal(3, validator.ExpectedDimensions);
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
        Assert.False(queryResult.Succeeded);
        Assert.False(deleteResult.Succeeded);
        Assert.Equal("Vector index name is required.", createResult.Reason);
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
    // Verifies that an undefined metric is rejected when the index is created instead of failing later during score interpretation.
    public async Task CreateIndexAsync_ShouldRejectUndefinedDistanceMetric()
    {
        var vectorStore = new InMemoryRagVectorStore();

        var result = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
            Metric = (VectorDistanceMetric)99,
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Vector distance metric is not defined.", result.Reason);
    }

    // Verifies that a null upsert request is treated as a programming error and fails fast with an exception
    // instead of producing a failure result.
    [Fact]
    public async Task UpsertAsync_ShouldThrow_WhenRequestIsNull()
    {
        var vectorStore = await CreateVectorStoreAsync();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            vectorStore.UpsertAsync(null!));

        Assert.Equal("request", exception.ParamName);
    }

    // Verifies that a request whose records are null-normalized or empty fails deterministically with a
    // validation error code and zero attempted/failed counts.
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
        Assert.Equal(VectorStoreUpsertErrorCode.ValidationFailed, emptyRecordsResult.ErrorCode);
        Assert.Equal(0, emptyRecordsResult.ProcessedCount);
        Assert.Equal(0, emptyRecordsResult.AttemptedCount);
        Assert.Equal(0, emptyRecordsResult.FailedCount);
        Assert.False(emptyRecordsResult.SupportsPartialSuccess);
        Assert.Empty(emptyRecordsResult.VectorIds);
    }

    // Verifies that a record with an invalid identifier is rejected with the standard validation failure
    // contract and that the whole batch is reported as failed.
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
        Assert.Equal(VectorStoreUpsertErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(1, result.AttemptedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.False(result.SupportsPartialSuccess);
        Assert.Empty(result.VectorIds);
    }

    // Verifies that records with null or empty vector values are rejected with the standard validation failure
    // contract before anything is written.
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
        Assert.Equal(VectorStoreUpsertErrorCode.ValidationFailed, emptyValuesResult.ErrorCode);
        Assert.Equal(0, emptyValuesResult.ProcessedCount);
        Assert.Equal(1, emptyValuesResult.AttemptedCount);
        Assert.Equal(1, emptyValuesResult.FailedCount);
        Assert.False(emptyValuesResult.SupportsPartialSuccess);
        Assert.Empty(emptyValuesResult.VectorIds);
    }

    // Verifies that a null or empty query vector is rejected deterministically before a vector search is performed.
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

    // Verifies that a non-positive TopK is rejected deterministically with a failure result and no matches.
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

    private static RetrievalMetadataFilter CreateFilter(params (string Key, string Value)[] criteria)
    {
        return new RetrievalMetadataFilter(
            criteria.Select(criterion => new RetrievalMetadataFilterCriterion(criterion.Key, criterion.Value)));
    }

    private sealed class TrackingDimensionValidator : IRagVectorRecordDimensionValidator
    {
        public bool WasCalled { get; private set; }

        public string IndexName { get; private set; } = string.Empty;

        public int ExpectedDimensions { get; private set; }

        public VectorRecordDimensionValidationResult Validate(
            UpsertVectorRequest request,
            int expectedDimensions)
        {
            WasCalled = true;
            IndexName = request.IndexName;
            ExpectedDimensions = expectedDimensions;

            return new VectorRecordDimensionValidationResult
            {
                Succeeded = true,
                IndexName = request.IndexName,
                ExpectedDimensions = expectedDimensions,
            };
        }
    }
}

