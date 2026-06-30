using Runiq.Rag.Embeddings;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Tests.Embeddings;

public sealed class DefaultRagEmbeddingInputPreparerTests
{
    [Fact]
    public async Task PrepareAsync_ShouldCreateEmbeddingInputFromValidChunk()
    {
        var chunk = CreateChunk();
        var preparer = new DefaultRagEmbeddingInputPreparer();

        var input = await preparer.PrepareAsync(chunk);

        Assert.Equal("chunk-1", input.Id);
        Assert.Equal("chunk-1", input.ChunkId);
        Assert.Equal("document-1", input.DocumentId);
        Assert.Equal(3, input.ChunkIndex);
        Assert.Equal("Content used as embedding input.", input.Content);
    }

    [Fact]
    public async Task PrepareAsync_ShouldCopyChunkMetadata()
    {
        var chunk = CreateChunk();
        var preparer = new DefaultRagEmbeddingInputPreparer();

        var input = await preparer.PrepareAsync(chunk);

        Assert.NotSame(chunk.Metadata, input.Metadata);
        Assert.Equal(10, input.Metadata.StartIndex);
        Assert.Equal(42, input.Metadata.EndIndex);
        Assert.Equal(8, input.Metadata.TokenCount);
        Assert.Equal("overview", input.Metadata.AdditionalMetadata.Values["section"]);
    }

    [Fact]
    public async Task PrepareAsync_ShouldCopyMetadataIndependentlyFromSourceChunk()
    {
        var chunk = CreateChunk();
        var originalMetadata = chunk.Metadata.AdditionalMetadata.Values["section"];
        var preparer = new DefaultRagEmbeddingInputPreparer();

        var input = await preparer.PrepareAsync(chunk);
        input.Metadata.AdditionalMetadata.Values["section"] = "changed";
        input.Metadata.AdditionalMetadata.Values["new"] = "value";

        Assert.NotSame(chunk.Metadata.AdditionalMetadata, input.Metadata.AdditionalMetadata);
        Assert.Equal(originalMetadata, chunk.Metadata.AdditionalMetadata.Values["section"]);
        Assert.False(chunk.Metadata.AdditionalMetadata.Values.ContainsKey("new"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PrepareAsync_ShouldPreserveEmptyOrWhitespaceContent(string content)
    {
        var chunk = CreateChunk(content);
        var preparer = new DefaultRagEmbeddingInputPreparer();

        var input = await preparer.PrepareAsync(chunk);

        Assert.Equal(content, input.Content);
    }

    [Fact]
    public async Task PrepareAsync_ShouldThrowForNullChunk()
    {
        var preparer = new DefaultRagEmbeddingInputPreparer();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => preparer.PrepareAsync(null!));

        Assert.Equal("chunk", exception.ParamName);
    }

    [Fact]
    public async Task PrepareAsync_ShouldObserveCancellation()
    {
        var preparer = new DefaultRagEmbeddingInputPreparer();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            preparer.PrepareAsync(CreateChunk(), cancellationTokenSource.Token));
    }

    private static RagChunk CreateChunk(string content = "Content used as embedding input.")
    {
        return new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
            Index = 3,
            Content = content,
            Metadata = new RagChunkMetadata
            {
                StartIndex = 10,
                EndIndex = 42,
                TokenCount = 8,
                AdditionalMetadata = new RagMetadata(new Dictionary<string, string>
                {
                    ["section"] = "overview",
                }),
            },
        };
    }
}
