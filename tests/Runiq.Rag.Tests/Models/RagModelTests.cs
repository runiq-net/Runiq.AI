using Runiq.Rag.Models.Context;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;

namespace Runiq.Rag.Tests.Models;

public sealed class RagModelTests
{
    [Fact]
    public void RagMetadata_DefaultValues_ShouldNotBeNullAndShouldBeEmpty()
    {
        var metadata = new RagMetadata();

        Assert.NotNull(metadata.Values);
        Assert.Empty(metadata.Values);
    }

    [Fact]
    public void RagMetadata_Constructor_ShouldCopyValues()
    {
        var values = new Dictionary<string, string>
        {
            ["source"] = "docs",
        };

        var metadata = new RagMetadata(values);

        values["source"] = "updated";

        Assert.Equal("docs", metadata.Values["source"]);
    }

    [Fact]
    public void RagQuery_DefaultTopK_ShouldBeFive()
    {
        var query = new RagQuery
        {
            Text = "What is Runiq?",
        };

        Assert.Equal(5, query.TopK);
    }

    [Fact]
    public void RagDocument_DefaultChunks_ShouldNotBeNullAndShouldHoldChunks()
    {
        var document = new RagDocument
        {
            Id = "document-1",
        };

        document.Chunks.Add(new RagChunk
        {
            Id = "chunk-1",
            DocumentId = document.Id,
        });

        Assert.NotNull(document.Chunks);
        Assert.Single(document.Chunks);
    }

    [Fact]
    public void RagDocument_DefaultMetadata_ShouldNotBeNull()
    {
        var document = new RagDocument
        {
            Id = "document-1",
        };

        Assert.NotNull(document.Metadata);
    }

    [Fact]
    public void RagChunk_DefaultMetadata_ShouldNotBeNull()
    {
        var chunk = new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
        };

        Assert.NotNull(chunk.Metadata);
    }

    [Fact]
    public void RagSearchResult_DefaultMetadata_ShouldNotBeNull()
    {
        var searchResult = new RagSearchResult
        {
            Chunk = new RagChunk
            {
                Id = "chunk-1",
                DocumentId = "document-1",
            },
        };

        Assert.NotNull(searchResult.Metadata);
    }

    [Fact]
    public void RagContext_DefaultResults_ShouldNotBeNullAndShouldHoldSearchResults()
    {
        var context = new RagContext
        {
            Query = new RagQuery
            {
                Text = "What is Runiq?",
            },
        };

        context.Results.Add(new RagSearchResult
        {
            Chunk = new RagChunk
            {
                Id = "chunk-1",
                DocumentId = "document-1",
            },
        });

        Assert.NotNull(context.Results);
        Assert.Single(context.Results);
    }

    [Fact]
    public void RagContext_DefaultMetadata_ShouldNotBeNull()
    {
        var context = new RagContext
        {
            Query = new RagQuery
            {
                Text = "What is Runiq?",
            },
        };

        Assert.NotNull(context.Metadata);
    }
}
