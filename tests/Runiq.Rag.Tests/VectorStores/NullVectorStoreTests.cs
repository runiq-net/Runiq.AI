using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.VectorStores;

namespace Runiq.Rag.Tests.VectorStores;

public sealed class NullVectorStoreTests
{
    private const string InvalidIndexNameReason = "Vector index name is required.";

    [Fact]
    public async Task CreateIndexAsync_ShouldReturnSuccessfulResult()
    {
        var vectorStore = new NullVectorStore();
        var request = new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 1536,
        };

        var result = await vectorStore.CreateIndexAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal(string.Empty, result.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateIndexAsync_ShouldFailDeterministically_WhenIndexNameIsInvalid(string? indexName)
    {
        var vectorStore = new NullVectorStore();

        var result = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = indexName!,
            Dimensions = 1536,
        });

        Assert.False(result.Succeeded);
        Assert.Equal(InvalidIndexNameReason, result.Reason);
    }

    [Fact]
    public async Task UpsertAsync_ShouldReturnSuccessfulResult()
    {
        var vectorStore = new NullVectorStore();
        var request = new UpsertVectorRequest
        {
            IndexName = "documents",
            Records =
            [
                new VectorRecord
                {
                    Id = "vector-1",
                    Values = [0.1f, 0.2f],
                },
            ],
        };

        var result = await vectorStore.UpsertAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.UpsertedCount);
        Assert.Equal("vector-1", Assert.Single(result.VectorIds));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpsertAsync_ShouldFailFast_WhenRequestIndexNameIsInvalid(string? indexName)
    {
        var exception = Assert.Throws<ArgumentException>(() => new UpsertVectorRequest
        {
            IndexName = indexName!,
            Records =
            [
                new VectorRecord
                {
                    Id = "vector-1",
                    Values = [0.1f, 0.2f],
                },
            ],
        });

        Assert.Equal(nameof(UpsertVectorRequest.IndexName), exception.ParamName);
    }

    [Fact]
    public async Task ChunkUpsertAsync_ShouldReturnSuccessfulResult()
    {
        var vectorStore = new NullVectorStore();
        var chunk = new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
        };
        var embedding = new RagEmbedding([0.1f, 0.2f]);

        var result = await vectorStore.UpsertAsync("documents", chunk, embedding);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.UpsertedCount);
        Assert.Equal("chunk-1", Assert.Single(result.VectorIds));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ChunkUpsertAsync_ShouldFailDeterministically_WhenIndexNameIsInvalid(string? indexName)
    {
        var vectorStore = new NullVectorStore();
        var chunk = new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
        };
        var embedding = new RagEmbedding([0.1f, 0.2f]);

        var result = await vectorStore.UpsertAsync(indexName!, chunk, embedding);

        Assert.False(result.Succeeded);
        Assert.Equal(InvalidIndexNameReason, result.Reason);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnSuccessfulEmptyResult()
    {
        var vectorStore = new NullVectorStore();
        var request = new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [0.1f, 0.2f],
            TopK = 3,
        };

        var result = await vectorStore.QueryAsync(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Records);
        Assert.Empty(result.Records);
        Assert.Equal(string.Empty, result.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task QueryAsync_ShouldFailDeterministically_WhenIndexNameIsInvalid(string? indexName)
    {
        var vectorStore = new NullVectorStore();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = indexName!,
            Values = [0.1f, 0.2f],
            TopK = 3,
        });

        Assert.False(result.Succeeded);
        Assert.Equal(InvalidIndexNameReason, result.Reason);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnSuccessfulDeterministicNotFoundResult()
    {
        var vectorStore = new NullVectorStore();
        var request = new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = ["vector-1", "vector-2"],
        };

        var result = await vectorStore.DeleteAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(0, result.DeletedCount);
        Assert.Empty(result.VectorIds);
        Assert.Equal(["vector-1", "vector-2"], result.NotFoundVectorIds);
        Assert.Equal(string.Empty, result.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteAsync_ShouldFailDeterministically_WhenIndexNameIsInvalid(string? indexName)
    {
        var vectorStore = new NullVectorStore();

        var result = await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = indexName!,
            VectorIds = ["vector-1", "vector-2"],
        });

        Assert.False(result.Succeeded);
        Assert.Equal(InvalidIndexNameReason, result.Reason);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnNonNullEmptyResults()
    {
        var vectorStore = new NullVectorStore();

        var results = await vectorStore.SearchAsync(
            new RagQuery { Text = "query" },
            new RagEmbedding());

        Assert.NotNull(results);
        Assert.Empty(results);
    }
}
