using System.Reflection;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Ingestion;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.VectorStores;

namespace Runiq.Rag.Tests.VectorStores;

public sealed class DefaultRagVectorRecordMapperTests
{
    [Fact]
    public void Map_ShouldPrepareVectorRecordForEmbeddedChunk()
    {
        var vectorValues = new[] { 0.1f, 0.2f, 0.3f };
        var item = CreateItem(vectorValues);
        var mapper = new DefaultRagVectorRecordMapper();

        var record = mapper.Map(item);

        Assert.Equal("document-1:chunk:7", record.Id);
        Assert.Equal("Chunk content.", record.Content);
        Assert.Same(vectorValues, record.Values);
    }

    [Fact]
    public void Map_ShouldUseDeterministicVectorRecordIdFromChunkId()
    {
        var item = CreateItem([0.1f]);
        var mapper = new DefaultRagVectorRecordMapper();

        var first = mapper.Map(item);
        var second = mapper.Map(item);

        Assert.Equal(item.Chunk.Id, first.Id);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void Map_ShouldCarryEmbeddingValuesWithoutProviderTransformation()
    {
        IReadOnlyList<float> vectorValues = [1.0f, -2.0f, 3.5f];
        var item = CreateItem(vectorValues);
        var mapper = new DefaultRagVectorRecordMapper();

        var record = mapper.Map(item);

        Assert.Same(vectorValues, record.Values);
        Assert.Equal(vectorValues, record.Values);
    }

    [Fact]
    public void Map_ShouldPreserveDocumentChunkAndOrderMetadata()
    {
        var item = CreateItem([0.1f]);
        var mapper = new DefaultRagVectorRecordMapper();

        var record = mapper.Map(item);

        Assert.Equal("document-1", record.Metadata.Values["documentId"]);
        Assert.Equal("document-1:chunk:7", record.Metadata.Values["chunkId"]);
        Assert.Equal("7", record.Metadata.Values["chunkIndex"]);
    }

    [Fact]
    public void Map_ShouldPreserveSourceMetadataWhenAvailable()
    {
        var item = CreateItem([0.1f]);
        var documentMetadata = new RagDocumentMetadata
        {
            SourceId = "source-1",
            SourceName = "handbook.md",
            SourceUri = "https://example.test/handbook",
            ContentType = "text/markdown",
        };
        var mapper = new DefaultRagVectorRecordMapper();

        var record = mapper.Map(item, documentMetadata);

        Assert.Equal("source-1", record.Metadata.Values["sourceId"]);
        Assert.Equal("handbook.md", record.Metadata.Values["sourceName"]);
        Assert.Equal("https://example.test/handbook", record.Metadata.Values["sourceUri"]);
        Assert.Equal("text/markdown", record.Metadata.Values["contentType"]);
    }

    [Fact]
    public void Map_ShouldPreferChunkMetadataOverDocumentMetadataForCustomKeyCollisions()
    {
        var item = CreateItem(
            [0.1f],
            chunkMetadata: new RagMetadata(new Dictionary<string, string>
            {
                ["tenant"] = "chunk-tenant",
                ["chunkOnly"] = "chunk-value",
                ["documentId"] = "spoofed-document",
                ["chunkId"] = "spoofed-chunk",
                ["chunkIndex"] = "999",
            }));
        var documentAdditionalMetadata = new RagMetadata(new Dictionary<string, string>
        {
            ["tenant"] = "document-tenant",
            ["documentOnly"] = "document-value",
            ["documentId"] = "document-custom",
            ["chunkId"] = "chunk-custom",
            ["chunkIndex"] = "123",
        });
        var documentMetadata = new RagDocumentMetadata
        {
            SourceId = "source-1",
            SourceName = "handbook.md",
            SourceUri = "https://example.test/handbook",
            AdditionalMetadata = documentAdditionalMetadata,
        };
        var mapper = new DefaultRagVectorRecordMapper();

        var record = mapper.Map(item, documentMetadata);

        Assert.Equal("chunk-tenant", record.Metadata.Values["tenant"]);
        Assert.Equal("document-value", record.Metadata.Values["documentOnly"]);
        Assert.Equal("chunk-value", record.Metadata.Values["chunkOnly"]);
        Assert.Equal("document-1", record.Metadata.Values["documentId"]);
        Assert.Equal("document-1:chunk:7", record.Metadata.Values["chunkId"]);
        Assert.Equal("7", record.Metadata.Values["chunkIndex"]);
        Assert.Equal("source-1", record.Metadata.Values["sourceId"]);
        Assert.Equal("handbook.md", record.Metadata.Values["sourceName"]);
        Assert.Equal("https://example.test/handbook", record.Metadata.Values["sourceUri"]);
        Assert.Equal("document-tenant", documentAdditionalMetadata.Values["tenant"]);
        Assert.Equal("chunk-tenant", item.Chunk.Metadata.AdditionalMetadata.Values["tenant"]);
    }

    [Fact]
    public void Map_ShouldCarryChunkMetadataIntoVectorMetadata()
    {
        var item = CreateItem([0.1f]);
        var mapper = new DefaultRagVectorRecordMapper();

        var record = mapper.Map(item);

        Assert.Equal("overview", record.Metadata.Values["section"]);
        Assert.Equal("10", record.Metadata.Values["startIndex"]);
        Assert.Equal("42", record.Metadata.Values["endIndex"]);
        Assert.Equal("8", record.Metadata.Values["tokenCount"]);
    }

    [Fact]
    public void Map_ShouldNotMutateSourceChunkMetadata()
    {
        var item = CreateItem([0.1f]);
        var originalMetadata = item.Chunk.Metadata.AdditionalMetadata;
        var mapper = new DefaultRagVectorRecordMapper();

        var record = mapper.Map(item);
        record.Metadata.Values["section"] = "changed";
        record.Metadata.Values["new"] = "value";

        Assert.NotSame(originalMetadata, record.Metadata);
        Assert.Equal("overview", originalMetadata.Values["section"]);
        Assert.False(originalMetadata.Values.ContainsKey("new"));
    }

    [Fact]
    public void Map_ShouldCreateIndependentMetadataInstance()
    {
        var item = CreateItem([0.1f]);
        var mapper = new DefaultRagVectorRecordMapper();

        var first = mapper.Map(item);
        var second = mapper.Map(item);

        Assert.NotSame(first.Metadata, second.Metadata);
        first.Metadata.Values["section"] = "changed";
        Assert.Equal("overview", second.Metadata.Values["section"]);
    }

    [Fact]
    public void Map_ShouldThrowDeterministicFailure_WhenEmbeddingIsMissing()
    {
        var item = CreateItem([0.1f]);
        typeof(RagChunkEmbeddingResult)
            .GetField("embedding", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(item.EmbeddingResult, null);
        var mapper = new DefaultRagVectorRecordMapper();

        var exception = Assert.Throws<ArgumentNullException>(() => mapper.Map(item));

        Assert.Equal("embedding", exception.ParamName);
    }

    [Fact]
    public void Map_ShouldThrowDeterministicFailure_WhenVectorIsEmpty()
    {
        var item = CreateItem(Array.Empty<float>());
        var mapper = new DefaultRagVectorRecordMapper();

        var exception = Assert.Throws<InvalidOperationException>(() => mapper.Map(item));

        Assert.Equal(
            "Vector record mapping requires a non-empty embedding vector for chunk 'document-1:chunk:7'.",
            exception.Message);
    }

    [Fact]
    public void Map_ShouldThrowDeterministicFailure_WhenVectorValuesAreNull()
    {
        var item = CreateItem(new RagEmbedding { Values = null! });
        var mapper = new DefaultRagVectorRecordMapper();

        var exception = Assert.Throws<InvalidOperationException>(() => mapper.Map(item));

        Assert.Equal(
            "Vector record mapping requires a non-empty embedding vector for chunk 'document-1:chunk:7'.",
            exception.Message);
    }

    [Fact]
    public void Map_ShouldRemainProviderIndependent()
    {
        var item = CreateItem([0.1f]);
        var mapper = new DefaultRagVectorRecordMapper();

        var record = mapper.Map(item);

        Assert.DoesNotContain(record.Metadata.Values.Keys, key => key.StartsWith("pinecone", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(record.Metadata.Values.Keys, key => key.StartsWith("qdrant", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(record.Metadata.Values.Keys, key => key.StartsWith("weaviate", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("document-1:chunk:7", record.Id);
    }

    [Fact]
    public void Map_ShouldThrowDeterministicFailure_WhenEmbeddingResultDoesNotMatchChunk()
    {
        var item = CreateItem(
            [0.1f],
            embeddingResult: new RagChunkEmbeddingResult
            {
                ChunkId = "other-chunk",
                DocumentId = "document-1",
                ChunkIndex = 7,
                Embedding = new RagEmbedding([0.1f]),
            });
        var mapper = new DefaultRagVectorRecordMapper();

        var exception = Assert.Throws<InvalidOperationException>(() => mapper.Map(item));

        Assert.Equal(
            "Vector record mapping expected embedding result for chunk 'document-1:chunk:7' but received 'other-chunk'.",
            exception.Message);
    }

    [Fact]
    public void MapMany_ShouldPreserveInputOrder()
    {
        var first = CreateItem([0.1f], chunkId: "document-1:chunk:0", chunkIndex: 0);
        var second = CreateItem([0.2f], chunkId: "document-1:chunk:1", chunkIndex: 1);
        var mapper = new DefaultRagVectorRecordMapper();

        var records = mapper.MapMany([first, second]);

        Assert.Equal(["document-1:chunk:0", "document-1:chunk:1"], records.Select(record => record.Id));
    }

    private static RagDocumentIngestionItem CreateItem(
        IReadOnlyList<float> values,
        string chunkId = "document-1:chunk:7",
        int chunkIndex = 7,
        RagMetadata? chunkMetadata = null,
        RagChunkEmbeddingResult? embeddingResult = null)
    {
        return CreateItem(
            new RagEmbedding(values),
            chunkId,
            chunkIndex,
            chunkMetadata,
            embeddingResult);
    }

    private static RagDocumentIngestionItem CreateItem(
        RagEmbedding embedding,
        string chunkId = "document-1:chunk:7",
        int chunkIndex = 7,
        RagMetadata? chunkMetadata = null,
        RagChunkEmbeddingResult? embeddingResult = null)
    {
        var chunk = new RagChunk
        {
            Id = chunkId,
            DocumentId = "document-1",
            Content = "Chunk content.",
            Index = chunkIndex,
            Metadata = new RagChunkMetadata
            {
                StartIndex = 10,
                EndIndex = 42,
                TokenCount = 8,
                AdditionalMetadata = chunkMetadata ?? new RagMetadata(new Dictionary<string, string>
                {
                    ["section"] = "overview",
                }),
            },
        };

        return new RagDocumentIngestionItem
        {
            Chunk = chunk,
            EmbeddingResult = embeddingResult ?? new RagChunkEmbeddingResult
            {
                ChunkId = chunk.Id,
                DocumentId = chunk.DocumentId,
                ChunkIndex = chunk.Index,
                Embedding = embedding,
            },
        };
    }
}
