using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.VectorStores;

namespace Runiq.AI.Rag.Tests.VectorStores;

public sealed class DefaultRagUpsertVectorRequestMapperTests
{
    [Fact]
    public void Map_ShouldConvertSingleEmbeddedChunkIntoSingleVectorRecord()
    {
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f, 0.2f]));
        var mapper = CreateMapper();

        var request = mapper.Map(ingestionResult, "documents-index");

        Assert.Single(request.Records);
    }

    [Fact]
    public void Map_ShouldConvertMultipleEmbeddedChunksIntoMultipleVectorRecords()
    {
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]),
            CreateItem("document-1", "document-1:chunk:1", 1, [0.2f]),
            CreateItem("document-1", "document-1:chunk:2", 2, [0.3f]));
        var mapper = CreateMapper();

        var request = mapper.Map(ingestionResult, "documents-index");

        Assert.Equal(3, request.Records.Count);
        Assert.Equal(
            ["document-1:chunk:0", "document-1:chunk:1", "document-1:chunk:2"],
            request.Records.Select(record => record.Id));
    }

    [Fact]
    public void Map_ShouldAssignRequestedIndexName()
    {
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));
        var mapper = CreateMapper();

        var request = mapper.Map(ingestionResult, "tenant-a-index");

        Assert.Equal("tenant-a-index", request.IndexName);
    }

    [Fact]
    public void Map_ShouldUseDeterministicVectorRecordIdFromChunkId()
    {
        var item = CreateItem("document-1", "document-1:chunk:5", 5, [0.1f]);
        var ingestionResult = CreateIngestionResult(item);
        var mapper = CreateMapper();

        var request = mapper.Map(ingestionResult, "documents-index");

        Assert.Equal(item.Chunk.Id, request.Records[0].Id);
    }

    [Fact]
    public void Map_ShouldCarryEmbeddingVectorValues()
    {
        IReadOnlyList<float> values = [1.0f, -2.0f, 3.5f];
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, values));
        var mapper = CreateMapper();

        var request = mapper.Map(ingestionResult, "documents-index");

        Assert.Equal(values, request.Records[0].Values);
    }

    [Fact]
    public void Map_ShouldPreserveDocumentMetadataInVectorRecordMetadata()
    {
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));
        var documentMetadata = new RagDocumentMetadata
        {
            SourceId = "source-1",
            SourceName = "handbook.md",
            SourceUri = "https://example.test/handbook",
            ContentType = "text/markdown",
        };
        var mapper = CreateMapper();

        var request = mapper.Map(ingestionResult, "documents-index", documentMetadata);

        var metadata = request.Records[0].Metadata.Values;
        Assert.Equal("source-1", metadata["sourceId"]);
        Assert.Equal("handbook.md", metadata["sourceName"]);
        Assert.Equal("https://example.test/handbook", metadata["sourceUri"]);
        Assert.Equal("text/markdown", metadata["contentType"]);
    }

    [Fact]
    public void Map_ShouldPreserveChunkMetadataInVectorRecordMetadata()
    {
        var chunkMetadata = new RagMetadata(new Dictionary<string, string>
        {
            ["section"] = "overview",
        });
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f], chunkMetadata));
        var mapper = CreateMapper();

        var request = mapper.Map(ingestionResult, "documents-index");

        Assert.Equal("overview", request.Records[0].Metadata.Values["section"]);
    }

    [Fact]
    public void Map_ShouldPreserveCanonicalMetadataFields()
    {
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:3", 3, [0.1f]));
        var mapper = CreateMapper();

        var request = mapper.Map(ingestionResult, "documents-index");

        var metadata = request.Records[0].Metadata.Values;
        Assert.Equal("document-1", metadata["documentId"]);
        Assert.Equal("document-1:chunk:3", metadata["chunkId"]);
        Assert.Equal("3", metadata["chunkIndex"]);
    }

    [Fact]
    public void Map_ShouldNotMutateSourceDocumentMetadata()
    {
        var ingestionResult = CreateIngestionResult(
            CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]));
        var documentAdditionalMetadata = new RagMetadata(new Dictionary<string, string>
        {
            ["tenant"] = "tenant-a",
        });
        var documentMetadata = new RagDocumentMetadata
        {
            AdditionalMetadata = documentAdditionalMetadata,
        };
        var mapper = CreateMapper();

        var request = mapper.Map(ingestionResult, "documents-index", documentMetadata);
        request.Records[0].Metadata.Values["tenant"] = "changed";
        request.Records[0].Metadata.Values["new"] = "value";

        Assert.Equal("tenant-a", documentAdditionalMetadata.Values["tenant"]);
        Assert.False(documentAdditionalMetadata.Values.ContainsKey("new"));
    }

    [Fact]
    public void Map_ShouldExcludeChunksWithoutAnEmbeddingResult()
    {
        var embeddedItem = CreateItem("document-1", "document-1:chunk:0", 0, [0.1f]);
        var unembeddedChunk = new RagChunk
        {
            Id = "document-1:chunk:1",
            DocumentId = "document-1",
            Content = "No embedding yet.",
            Index = 1,
        };
        var ingestionResult = new RagDocumentIngestionResult
        {
            DocumentId = "document-1",
            Chunks = [embeddedItem.Chunk, unembeddedChunk],
            Items = [embeddedItem],
        };
        var mapper = CreateMapper();

        var request = mapper.Map(ingestionResult, "documents-index");

        Assert.Single(request.Records);
        Assert.Equal("document-1:chunk:0", request.Records[0].Id);
    }

    [Fact]
    public void Map_ShouldReturnEmptyRecordsForEmptyIngestionOutput()
    {
        var ingestionResult = new RagDocumentIngestionResult
        {
            DocumentId = "document-1",
            Chunks = [],
            Items = [],
        };
        var mapper = CreateMapper();

        var request = mapper.Map(ingestionResult, "documents-index");

        Assert.Empty(request.Records);
        Assert.Equal("documents-index", request.IndexName);
    }

    private static DefaultRagUpsertVectorRequestMapper CreateMapper()
    {
        return new DefaultRagUpsertVectorRequestMapper(new DefaultRagVectorRecordMapper());
    }

    private static RagDocumentIngestionResult CreateIngestionResult(params RagDocumentIngestionItem[] items)
    {
        return new RagDocumentIngestionResult
        {
            DocumentId = items[0].Chunk.DocumentId,
            Chunks = items.Select(item => item.Chunk).ToList(),
            Items = items,
        };
    }

    private static RagDocumentIngestionItem CreateItem(
        string documentId,
        string chunkId,
        int chunkIndex,
        IReadOnlyList<float> vectorValues,
        RagMetadata? chunkMetadata = null)
    {
        var chunk = new RagChunk
        {
            Id = chunkId,
            DocumentId = documentId,
            Content = "Chunk content.",
            Index = chunkIndex,
            Metadata = new RagChunkMetadata
            {
                AdditionalMetadata = chunkMetadata ?? RagMetadata.Empty,
            },
        };

        return new RagDocumentIngestionItem
        {
            Chunk = chunk,
            EmbeddingResult = new RagChunkEmbeddingResult
            {
                ChunkId = chunk.Id,
                DocumentId = chunk.DocumentId,
                ChunkIndex = chunk.Index,
                Embedding = new RagEmbedding(vectorValues),
            },
        };
    }
}

