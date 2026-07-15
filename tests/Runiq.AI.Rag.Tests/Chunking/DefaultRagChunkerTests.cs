using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Chunking;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;

namespace Runiq.AI.Rag.Tests.Chunking;

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
        Assert.Equal([0, 1], chunks.Select(chunk => chunk.Index));
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
        Assert.Equal("3", chunk.Metadata.AdditionalMetadata.Values["tokenCount"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ChunkAsync_ShouldThrow_WhenMaxChunkLengthIsInvalid(int maxChunkLength)
    {
        var chunker = CreateChunker(maxChunkLength: maxChunkLength, chunkOverlap: 0);
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "content",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => chunker.ChunkAsync(document));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public async Task ChunkAsync_ShouldThrow_WhenChunkOverlapIsInvalid(int chunkOverlap)
    {
        var chunker = CreateChunker(maxChunkLength: 10, chunkOverlap: chunkOverlap);
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "content",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => chunker.ChunkAsync(document));
    }

    [Fact]
    public async Task ChunkAsync_ShouldPropagateDocumentMetadataToEveryChunk()
    {
        var createdAt = DateTimeOffset.Parse("2024-01-02T03:04:05+00:00");
        var updatedAt = DateTimeOffset.Parse("2024-02-03T04:05:06+00:00");
        var chunker = CreateChunker(maxChunkLength: 4, chunkOverlap: 0);
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "abcdefgh",
            Metadata = new RagDocumentMetadata
            {
                SourceId = "source-1",
                SourceName = "Product handbook",
                SourceUri = "https://example.test/handbook",
                ContentType = "text/markdown",
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                AdditionalMetadata = new RagMetadata(new Dictionary<string, string>
                {
                    ["tenant"] = "tenant-1",
                    ["category"] = "release-notes",
                }),
            },
        };

        var chunks = await chunker.ChunkAsync(document);

        Assert.All(chunks, chunk =>
        {
            var metadata = chunk.Metadata.AdditionalMetadata.Values;
            Assert.Equal("source-1", metadata["sourceId"]);
            Assert.Equal("Product handbook", metadata["sourceName"]);
            Assert.Equal("https://example.test/handbook", metadata["sourceUri"]);
            Assert.Equal("text/markdown", metadata["contentType"]);
            Assert.Equal("2024-01-02T03:04:05.0000000+00:00", metadata["createdAt"]);
            Assert.Equal("2024-02-03T04:05:06.0000000+00:00", metadata["updatedAt"]);
            Assert.Equal("tenant-1", metadata["tenant"]);
            Assert.Equal("release-notes", metadata["category"]);
        });
    }

    [Fact]
    public async Task ChunkAsync_ShouldLetChunkTechnicalMetadataWinOnAdditionalMetadataCollision()
    {
        var chunker = CreateChunker(maxChunkLength: 4, chunkOverlap: 1);
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "one two",
            Metadata = new RagDocumentMetadata
            {
                AdditionalMetadata = new RagMetadata(new Dictionary<string, string>
                {
                    ["StartIndex"] = "document-start",
                    ["ENDINDEX"] = "document-end",
                    ["tokenCount"] = "document-token-count",
                    ["nonConflicting"] = "preserved",
                }),
            },
        };

        var chunks = await chunker.ChunkAsync(document);

        Assert.Equal("0", chunks[0].Metadata.AdditionalMetadata.Values["startIndex"]);
        Assert.Equal("4", chunks[0].Metadata.AdditionalMetadata.Values["endIndex"]);
        Assert.Equal("1", chunks[0].Metadata.AdditionalMetadata.Values["tokenCount"]);
        Assert.Equal("preserved", chunks[0].Metadata.AdditionalMetadata.Values["nonConflicting"]);
        Assert.False(chunks[0].Metadata.AdditionalMetadata.Values.ContainsKey("StartIndex"));
        Assert.False(chunks[0].Metadata.AdditionalMetadata.Values.ContainsKey("ENDINDEX"));
    }

    [Fact]
    public async Task ChunkAsync_ShouldNotMutateDocumentMetadataAndShouldCreateIndependentChunkMetadata()
    {
        var documentMetadata = new RagMetadata(new Dictionary<string, string>
        {
            ["tenant"] = "tenant-1",
        });
        var chunker = CreateChunker(maxChunkLength: 4, chunkOverlap: 0);
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "abcdefgh",
            Metadata = new RagDocumentMetadata
            {
                AdditionalMetadata = documentMetadata,
            },
        };

        var chunks = await chunker.ChunkAsync(document);

        chunks[0].Metadata.AdditionalMetadata.Values["tenant"] = "changed";
        chunks[0].Metadata.AdditionalMetadata.Values["firstOnly"] = "value";

        Assert.Equal("tenant-1", document.Metadata.AdditionalMetadata.Values["tenant"]);
        Assert.False(document.Metadata.AdditionalMetadata.Values.ContainsKey("firstOnly"));
        Assert.Equal("tenant-1", chunks[1].Metadata.AdditionalMetadata.Values["tenant"]);
        Assert.False(chunks[1].Metadata.AdditionalMetadata.Values.ContainsKey("firstOnly"));
        Assert.NotSame(document.Metadata.AdditionalMetadata, chunks[0].Metadata.AdditionalMetadata);
        Assert.NotSame(chunks[0].Metadata.AdditionalMetadata, chunks[1].Metadata.AdditionalMetadata);
    }

    [Fact]
    public async Task ChunkAsync_ShouldUseValidMaxChunkLengthAndChunkOverlap()
    {
        var chunker = CreateChunker(maxChunkLength: 5, chunkOverlap: 2);
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "abcdefghijkl",
        };

        var chunks = await chunker.ChunkAsync(document);

        Assert.Equal(["abcde", "defgh", "ghijk", "jkl"], chunks.Select(chunk => chunk.Content));
        Assert.Equal([0, 3, 6, 9], chunks.Select(chunk => chunk.Metadata.StartIndex));
        Assert.Equal([5, 8, 11, 12], chunks.Select(chunk => chunk.Metadata.EndIndex));
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

