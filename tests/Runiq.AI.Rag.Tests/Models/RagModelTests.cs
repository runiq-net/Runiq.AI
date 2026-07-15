using Runiq.AI.Rag.Models.Context;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Rag.Tests.Models;

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
    public void RagDocument_Should_Preserve_Valid_Id()
    {
        var document = new RagDocument
        {
            Id = "document-1",
        };

        Assert.Equal("document-1", document.Id);
    }

    [Fact]
    public void RagDocument_Should_Reject_Null_Id()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RagDocument
        {
            Id = null!,
        });

        Assert.Equal("value", exception.ParamName);
        Assert.StartsWith("Document id cannot be null, empty, or whitespace.", exception.Message);
    }

    [Fact]
    public void RagDocument_Should_Reject_Empty_Id()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RagDocument
        {
            Id = string.Empty,
        });

        Assert.Equal("value", exception.ParamName);
        Assert.StartsWith("Document id cannot be null, empty, or whitespace.", exception.Message);
    }

    [Fact]
    public void RagDocument_Should_Reject_Whitespace_Id()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RagDocument
        {
            Id = "   ",
        });

        Assert.Equal("value", exception.ParamName);
        Assert.StartsWith("Document id cannot be null, empty, or whitespace.", exception.Message);
    }

    [Fact]
    public void RagDocument_Should_Preserve_Content_For_Chunking()
    {
        var document = new RagDocument
        {
            Id = "document-1",
            Content = "Content used by the chunking pipeline.",
        };

        Assert.Equal("Content used by the chunking pipeline.", document.Content);
    }

    [Fact]
    public void RagDocument_Should_Preserve_Source_Metadata()
    {
        var createdAt = new DateTimeOffset(2026, 6, 30, 10, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddHours(1);
        var metadata = new RagDocumentMetadata
        {
            SourceId = "source-1",
            SourceName = "Product handbook",
            SourceUri = "https://example.com/product-handbook",
            ContentType = "text/markdown",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

        var document = new RagDocument
        {
            Id = "document-1",
            Metadata = metadata,
        };

        Assert.Same(metadata, document.Metadata);
        Assert.Equal("source-1", document.Metadata.SourceId);
        Assert.Equal("Product handbook", document.Metadata.SourceName);
        Assert.Equal("https://example.com/product-handbook", document.Metadata.SourceUri);
        Assert.Equal("text/markdown", document.Metadata.ContentType);
        Assert.Equal(createdAt, document.Metadata.CreatedAt);
        Assert.Equal(updatedAt, document.Metadata.UpdatedAt);
    }

    [Fact]
    public void RagDocument_Should_Preserve_Custom_Metadata()
    {
        var additionalMetadata = new RagMetadata(new Dictionary<string, string>
        {
            ["tenant"] = "tenant-1",
            ["category"] = "release-notes",
        });

        var document = new RagDocument
        {
            Id = "document-1",
            Metadata = new RagDocumentMetadata
            {
                AdditionalMetadata = additionalMetadata,
            },
        };

        Assert.Same(additionalMetadata, document.Metadata.AdditionalMetadata);
        Assert.Equal("tenant-1", document.Metadata.AdditionalMetadata.Values["tenant"]);
        Assert.Equal("release-notes", document.Metadata.AdditionalMetadata.Values["category"]);
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
    public void RagDocument_Should_Handle_Null_Metadata_Deterministically()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new RagDocument
        {
            Id = "document-1",
            Metadata = null!,
        });

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void RagDocument_Should_Handle_Null_Content_Deterministically()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new RagDocument
        {
            Id = "document-1",
            Content = null!,
        });

        Assert.Equal("value", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RagDocument_Should_Handle_Empty_Content_Deterministically(string content)
    {
        var document = new RagDocument
        {
            Id = "document-1",
            Content = content,
        };

        Assert.Equal(content, document.Content);
    }

    [Fact]
    public void RagDocument_Should_Handle_Null_Chunks_Deterministically()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new RagDocument
        {
            Id = "document-1",
            Chunks = null!,
        });

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void RagDocument_Should_Keep_Chunks_Collection_Usable()
    {
        var chunk = new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
            Content = "chunk content",
        };

        var document = new RagDocument
        {
            Id = "document-1",
            Chunks = new List<RagChunk> { chunk },
        };

        Assert.Same(chunk, Assert.Single(document.Chunks));
    }

    [Fact]
    public void RagDocument_Should_Keep_Public_Model_Names()
    {
        Assert.Equal("RagDocument", typeof(RagDocument).Name);
        Assert.Equal("RagMetadata", typeof(RagMetadata).Name);
        Assert.Equal("RagChunk", typeof(RagChunk).Name);
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
    public void RagChunk_Should_Preserve_Contract_Values()
    {
        var metadata = new RagChunkMetadata
        {
            StartIndex = 10,
            EndIndex = 42,
            TokenCount = 8,
            AdditionalMetadata = new RagMetadata(new Dictionary<string, string>
            {
                ["section"] = "overview",
            }),
        };

        var chunk = new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
            Index = 2,
            Content = "Text used as embedding input.",
            Metadata = metadata,
        };

        Assert.Equal("document-1", chunk.DocumentId);
        Assert.Equal("chunk-1", chunk.Id);
        Assert.Equal(2, chunk.Index);
        Assert.Equal("Text used as embedding input.", chunk.Content);
        Assert.Same(metadata, chunk.Metadata);
        Assert.Equal(10, chunk.Metadata.StartIndex);
        Assert.Equal(42, chunk.Metadata.EndIndex);
        Assert.Equal(8, chunk.Metadata.TokenCount);
        Assert.Equal("overview", chunk.Metadata.AdditionalMetadata.Values["section"]);
    }

    [Fact]
    public void RagChunk_Should_Reject_Null_Id()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RagChunk
        {
            Id = null!,
            DocumentId = "document-1",
        });

        Assert.Equal("value", exception.ParamName);
        Assert.StartsWith("Chunk id cannot be null, empty, or whitespace.", exception.Message);
    }

    [Fact]
    public void RagChunk_Should_Reject_Empty_Id()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RagChunk
        {
            Id = string.Empty,
            DocumentId = "document-1",
        });

        Assert.Equal("value", exception.ParamName);
        Assert.StartsWith("Chunk id cannot be null, empty, or whitespace.", exception.Message);
    }

    [Fact]
    public void RagChunk_Should_Reject_Whitespace_Id()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RagChunk
        {
            Id = "   ",
            DocumentId = "document-1",
        });

        Assert.Equal("value", exception.ParamName);
        Assert.StartsWith("Chunk id cannot be null, empty, or whitespace.", exception.Message);
    }

    [Fact]
    public void RagChunk_Should_Reject_Null_DocumentId()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RagChunk
        {
            Id = "chunk-1",
            DocumentId = null!,
        });

        Assert.Equal("value", exception.ParamName);
        Assert.StartsWith("Chunk document id cannot be null, empty, or whitespace.", exception.Message);
    }

    [Fact]
    public void RagChunk_Should_Reject_Empty_DocumentId()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RagChunk
        {
            Id = "chunk-1",
            DocumentId = string.Empty,
        });

        Assert.Equal("value", exception.ParamName);
        Assert.StartsWith("Chunk document id cannot be null, empty, or whitespace.", exception.Message);
    }

    [Fact]
    public void RagChunk_Should_Reject_Whitespace_DocumentId()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "   ",
        });

        Assert.Equal("value", exception.ParamName);
        Assert.StartsWith("Chunk document id cannot be null, empty, or whitespace.", exception.Message);
    }

    [Fact]
    public void RagChunk_Should_Handle_Null_Content_Deterministically()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
            Content = null!,
        });

        Assert.Equal("value", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RagChunk_Should_Handle_Empty_Content_Deterministically(string content)
    {
        var chunk = new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
            Content = content,
        };

        Assert.Equal(content, chunk.Content);
    }

    [Fact]
    public void RagChunk_Should_Handle_Null_Metadata_Deterministically()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
            Metadata = null!,
        });

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void RagChunkMetadata_DefaultAdditionalMetadata_ShouldNotBeNull()
    {
        var metadata = new RagChunkMetadata();

        Assert.NotNull(metadata.AdditionalMetadata);
        Assert.Empty(metadata.AdditionalMetadata.Values);
    }

    [Fact]
    public void RagChunkMetadata_Should_Handle_Null_AdditionalMetadata_Deterministically()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new RagChunkMetadata
        {
            AdditionalMetadata = null!,
        });

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void RagChunk_Should_Remain_Provider_And_Storage_Independent()
    {
        var properties = typeof(RagChunk).GetProperties();

        Assert.Equal(
            ["Id", "DocumentId", "Content", "Index", "Metadata"],
            properties.Select(property => property.Name));
        Assert.All(properties, property =>
        {
            var propertyType = property.PropertyType;

            Assert.DoesNotContain("Provider", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Client", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Vector", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                propertyType == typeof(string)
                    || propertyType == typeof(int)
                    || propertyType == typeof(RagChunkMetadata),
                $"RagChunk property '{property.Name}' uses provider or storage-specific type '{propertyType.FullName}'.");
        });
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

