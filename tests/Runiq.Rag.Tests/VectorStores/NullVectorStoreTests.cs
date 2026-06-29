using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.VectorStores;

namespace Runiq.Rag.Tests.VectorStores;

public sealed class NullVectorStoreTests
{
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

    [Fact]
    public async Task LegacyUpsertAsync_ShouldReturnSuccessfulResult()
    {
        var vectorStore = new NullVectorStore();
        var chunk = new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
        };
        var embedding = new RagEmbedding([0.1f, 0.2f]);

        var result = await vectorStore.UpsertAsync(chunk, embedding);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.UpsertedCount);
        Assert.Equal("chunk-1", Assert.Single(result.VectorIds));
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
