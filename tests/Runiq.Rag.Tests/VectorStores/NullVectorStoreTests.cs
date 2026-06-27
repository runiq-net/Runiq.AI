using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.VectorStores;

namespace Runiq.Rag.Tests.VectorStores;

public sealed class NullVectorStoreTests
{
    [Fact]
    public async Task UpsertAsync_ShouldCompleteWithoutThrowing()
    {
        var vectorStore = new NullVectorStore();
        var chunk = new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
        };
        var embedding = new RagEmbedding();

        var exception = await Record.ExceptionAsync(() => vectorStore.UpsertAsync(chunk, embedding));

        Assert.Null(exception);
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
