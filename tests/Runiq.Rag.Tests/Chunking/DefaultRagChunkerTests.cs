using Microsoft.Extensions.Options;
using Runiq.Rag.Chunking;
using Runiq.Rag.Configuration;
using Runiq.Rag.Models.Documents;

namespace Runiq.Rag.Tests.Chunking;

public sealed class DefaultRagChunkerTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenOptionsIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new DefaultRagChunker(null!));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public async Task ChunkAsync_ShouldReturnEmptyList_WhenContentIsEmpty()
    {
        var chunker = CreateChunker();
        var document = new RagDocument
        {
            Id = "document-1",
        };

        var chunks = await chunker.ChunkAsync(document);

        Assert.NotNull(chunks);
        Assert.Empty(chunks);
    }

    [Fact]
    public async Task ChunkAsync_ShouldCreateSingleChunk_WhenContentFitsMaxLength()
    {
        var chunker = CreateChunker(maxChunkLength: 20, chunkOverlap: 5);
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "short text",
        };

        var chunks = await chunker.ChunkAsync(document);

        var chunk = Assert.Single(chunks);
        Assert.Equal("document-1", chunk.DocumentId);
        Assert.Equal("short text", chunk.Content);
        Assert.Equal(0, chunk.Index);
        Assert.Equal(0, chunk.Metadata.StartIndex);
        Assert.Equal(10, chunk.Metadata.EndIndex);
    }

    [Fact]
    public async Task ChunkAsync_ShouldSplitContentIntoOrderedChunks()
    {
        var chunker = CreateChunker(maxChunkLength: 4, chunkOverlap: 1);
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "abcdefghij",
        };

        var chunks = await chunker.ChunkAsync(document);

        Assert.Collection(
            chunks,
            chunk =>
            {
                Assert.Equal("abcd", chunk.Content);
                Assert.Equal(0, chunk.Index);
                Assert.Equal(0, chunk.Metadata.StartIndex);
                Assert.Equal(4, chunk.Metadata.EndIndex);
            },
            chunk =>
            {
                Assert.Equal("defg", chunk.Content);
                Assert.Equal(1, chunk.Index);
                Assert.Equal(3, chunk.Metadata.StartIndex);
                Assert.Equal(7, chunk.Metadata.EndIndex);
            },
            chunk =>
            {
                Assert.Equal("ghij", chunk.Content);
                Assert.Equal(2, chunk.Index);
                Assert.Equal(6, chunk.Metadata.StartIndex);
                Assert.Equal(10, chunk.Metadata.EndIndex);
            });
    }

    [Fact]
    public async Task ChunkAsync_ShouldPreserveDocumentIdAndAssignChunkIds()
    {
        var chunker = CreateChunker(maxChunkLength: 4, chunkOverlap: 0);
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "abcdef",
        };

        var chunks = await chunker.ChunkAsync(document);

        Assert.All(chunks, chunk => Assert.Equal("document-1", chunk.DocumentId));
        Assert.Equal("document-1:chunk:0", chunks[0].Id);
        Assert.Equal("document-1:chunk:1", chunks[1].Id);
    }

    [Fact]
    public async Task ChunkAsync_ShouldPopulateApproximateTokenCount()
    {
        var chunker = CreateChunker(maxChunkLength: 100, chunkOverlap: 0);
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "one two  three",
        };

        var chunk = Assert.Single(await chunker.ChunkAsync(document));

        Assert.Equal(3, chunk.Metadata.TokenCount);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, -1)]
    [InlineData(10, 10)]
    public async Task ChunkAsync_ShouldThrow_WhenChunkingOptionsAreInvalid(
        int maxChunkLength,
        int chunkOverlap)
    {
        var chunker = CreateChunker(maxChunkLength, chunkOverlap);
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "content",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => chunker.ChunkAsync(document));
    }

    private static DefaultRagChunker CreateChunker(
        int maxChunkLength = 1000,
        int chunkOverlap = 100)
    {
        return new DefaultRagChunker(Options.Create(new RagOptions
        {
            Chunking = new RagChunkingOptions
            {
                MaxChunkLength = maxChunkLength,
                ChunkOverlap = chunkOverlap,
            },
        }));
    }
}
