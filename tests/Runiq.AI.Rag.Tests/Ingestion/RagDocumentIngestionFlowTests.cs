using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Chunking;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Embeddings;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Services;

namespace Runiq.AI.Rag.Tests.Ingestion;

public sealed class RagDocumentIngestionFlowTests
{
    [Fact]
    public async Task IngestAsync_ShouldChunkDocumentPropagateMetadataAndAssociateDeterministicEmbeddings()
    {
        var documentMetadata = new RagMetadata(new Dictionary<string, string>
        {
            ["tenant"] = "tenant-1",
            ["category"] = "manual",
        });
        var document = CreateDocument("document-1", "abcdefghij", documentMetadata);
        var provider = new DeterministicTrackingEmbeddingProvider();
        var inputPreparer = new TrackingEmbeddingInputPreparer();
        var service = CreateService(provider, inputPreparer, maxChunkLength: 4, chunkOverlap: 1);

        var result = await service.IngestAsync(document);

        Assert.Equal("document-1", result.DocumentId);
        Assert.Equal(["abcd", "defg", "ghij"], result.Chunks.Select(chunk => chunk.Content));
        Assert.Equal([0, 1, 2], result.Chunks.Select(chunk => chunk.Index));
        Assert.Equal(["document-1:chunk:0", "document-1:chunk:1", "document-1:chunk:2"], result.Chunks.Select(chunk => chunk.Id));
        Assert.All(result.Chunks, chunk => Assert.Equal("document-1", chunk.DocumentId));
        Assert.Equal(["abcd", "defg", "ghij"], provider.Texts);

        Assert.Collection(
            result.Items,
            item => AssertChunkEmbeddingAssociation(item.Chunk, item.EmbeddingResult, "abcd"),
            item => AssertChunkEmbeddingAssociation(item.Chunk, item.EmbeddingResult, "defg"),
            item => AssertChunkEmbeddingAssociation(item.Chunk, item.EmbeddingResult, "ghij"));

        Assert.Collection(
            inputPreparer.Inputs,
            input => AssertEmbeddingInput(input, result.Chunks[0], "tenant-1", "manual"),
            input => AssertEmbeddingInput(input, result.Chunks[1], "tenant-1", "manual"),
            input => AssertEmbeddingInput(input, result.Chunks[2], "tenant-1", "manual"));

        result.Chunks[0].Metadata.AdditionalMetadata.Values["tenant"] = "changed";
        result.Chunks[0].Metadata.AdditionalMetadata.Values["chunkOnly"] = "value";

        Assert.Equal("tenant-1", document.Metadata.AdditionalMetadata.Values["tenant"]);
        Assert.False(document.Metadata.AdditionalMetadata.Values.ContainsKey("chunkOnly"));
        Assert.Equal("tenant-1", inputPreparer.Inputs[0].Metadata.AdditionalMetadata.Values["tenant"]);
        Assert.NotSame(document.Metadata.AdditionalMetadata, result.Chunks[0].Metadata.AdditionalMetadata);
        Assert.NotSame(result.Chunks[0].Metadata.AdditionalMetadata, inputPreparer.Inputs[0].Metadata.AdditionalMetadata);
    }

    [Fact]
    public async Task IngestAsync_ShouldReturnEmptyResultWithoutEmbeddingProviderCalls_WhenDocumentContentIsEmpty()
    {
        var provider = new DeterministicTrackingEmbeddingProvider();
        var inputPreparer = new TrackingEmbeddingInputPreparer();
        var service = CreateService(provider, inputPreparer, maxChunkLength: 4, chunkOverlap: 1);

        var result = await service.IngestAsync(CreateDocument("document-1", string.Empty));

        Assert.Equal("document-1", result.DocumentId);
        Assert.Empty(result.Chunks);
        Assert.Empty(result.Items);
        Assert.Empty(inputPreparer.Inputs);
        Assert.Empty(provider.Texts);
    }

    [Fact]
    public async Task IngestAsync_ShouldUseConfiguredChunkOverlap_WhenDefaultChunkerRunsInTheIngestionFlow()
    {
        var provider = new DeterministicTrackingEmbeddingProvider();
        var service = CreateService(provider, new TrackingEmbeddingInputPreparer(), maxChunkLength: 5, chunkOverlap: 2);

        var result = await service.IngestAsync(CreateDocument("document-1", "abcdefghijkl"));

        Assert.Equal(["abcde", "defgh", "ghijk", "jkl"], result.Chunks.Select(chunk => chunk.Content));
        Assert.Equal([0, 3, 6, 9], result.Chunks.Select(chunk => chunk.Metadata.StartIndex));
        Assert.Equal([5, 8, 11, 12], result.Chunks.Select(chunk => chunk.Metadata.EndIndex));
        Assert.Equal(["abcde", "defgh", "ghijk", "jkl"], provider.Texts);
    }

    [Fact]
    public async Task IngestAsync_ShouldSurfaceExistingChunkingFailure_WhenChunkingOptionsAreInvalid()
    {
        var service = CreateService(
            new DeterministicTrackingEmbeddingProvider(),
            new TrackingEmbeddingInputPreparer(),
            maxChunkLength: 4,
            chunkOverlap: 4);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestAsync(CreateDocument("document-1", "content")));

        Assert.Equal("RAG document ingestion failed while chunking document 'document-1'.", exception.Message);
        Assert.Equal("RAG chunking ChunkOverlap must be smaller than MaxChunkLength.", exception.InnerException?.Message);
    }

    [Fact]
    public async Task AddRuniqRag_ShouldRunIngestionWithFakeEmbeddingProviderWithoutExternalDependencies()
    {
        var provider = new DeterministicTrackingEmbeddingProvider();
        var inputPreparer = new TrackingEmbeddingInputPreparer();
        var services = new ServiceCollection();

        services.AddSingleton<IRagEmbeddingProvider>(provider);
        services.AddSingleton<IRagEmbeddingInputPreparer>(inputPreparer);
        services.AddRuniqRag();
        services.Configure<RagOptions>(options =>
        {
            options.Chunking.MaxChunkLength = 4;
            options.Chunking.ChunkOverlap = 0;
        });

        using var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetRequiredService<IRagDocumentIngestionService>();

        var result = await service.IngestAsync(CreateDocument("document-1", "abcdefgh"));

        Assert.Equal(["abcd", "efgh"], provider.Texts);
        Assert.Equal(["document-1:chunk:0", "document-1:chunk:1"], inputPreparer.Inputs.Select(input => input.ChunkId));
        Assert.Equal(provider.Texts.Count, result.Items.Count);
    }

    private static DefaultRagDocumentIngestionService CreateService(
        IRagEmbeddingProvider provider,
        IRagEmbeddingInputPreparer inputPreparer,
        int maxChunkLength,
        int chunkOverlap)
    {
        // Builds the production ingestion chain with a test provider so the flow stays end-to-end without external calls.
        var options = Options.Create(new RagOptions
        {
            Chunking = new RagChunkingOptions
            {
                MaxChunkLength = maxChunkLength,
                ChunkOverlap = chunkOverlap,
            },
        });
        var chunker = new DefaultRagChunker(options);
        var generator = new DefaultRagChunkEmbeddingGenerator(provider, inputPreparer);

        return new DefaultRagDocumentIngestionService(chunker, generator);
    }

    private static RagDocument CreateDocument(
        string id,
        string content,
        RagMetadata? additionalMetadata = null)
    {
        // Keeps document identity, content, and metadata stable across regression tests.
        return new RagDocument
        {
            Id = id,
            Content = content,
            Metadata = new RagDocumentMetadata
            {
                SourceId = "source-1",
                SourceName = "Product handbook",
                SourceUri = "https://example.test/product-handbook",
                ContentType = "text/plain",
                AdditionalMetadata = additionalMetadata ?? new RagMetadata(),
            },
        };
    }

    private static void AssertChunkEmbeddingAssociation(
        RagChunk chunk,
        RagChunkEmbeddingResult embeddingResult,
        string expectedContent)
    {
        // Centralizes the chunk-to-embedding checks so each assertion path validates the same correlation contract.
        Assert.Equal(chunk.Id, embeddingResult.ChunkId);
        Assert.Equal(chunk.DocumentId, embeddingResult.DocumentId);
        Assert.Equal(chunk.Index, embeddingResult.ChunkIndex);
        Assert.Equal(DeterministicTrackingEmbeddingProvider.CreateEmbeddingValues(expectedContent), embeddingResult.Embedding.Values);
    }

    private static void AssertEmbeddingInput(
        RagEmbeddingInput input,
        RagChunk chunk,
        string expectedTenant,
        string expectedCategory)
    {
        // Keeps metadata propagation assertions readable while verifying the prepared input still matches its source chunk.
        Assert.Equal(chunk.Id, input.Id);
        Assert.Equal(chunk.Id, input.ChunkId);
        Assert.Equal(chunk.DocumentId, input.DocumentId);
        Assert.Equal(chunk.Index, input.ChunkIndex);
        Assert.Equal(chunk.Content, input.Content);
        Assert.Equal(chunk.Metadata.StartIndex, input.Metadata.StartIndex);
        Assert.Equal(chunk.Metadata.EndIndex, input.Metadata.EndIndex);
        Assert.Equal(chunk.Metadata.TokenCount, input.Metadata.TokenCount);
        Assert.Equal(expectedTenant, input.Metadata.AdditionalMetadata.Values["tenant"]);
        Assert.Equal(expectedCategory, input.Metadata.AdditionalMetadata.Values["category"]);
        Assert.Equal("source-1", input.Metadata.AdditionalMetadata.Values["sourceId"]);
        Assert.Equal("Product handbook", input.Metadata.AdditionalMetadata.Values["sourceName"]);
        Assert.Equal("text/plain", input.Metadata.AdditionalMetadata.Values["contentType"]);
    }

    private sealed class TrackingEmbeddingInputPreparer : IRagEmbeddingInputPreparer
    {
        private readonly DefaultRagEmbeddingInputPreparer inner = new();

        // This helper keeps the production input-preparation behavior while exposing the prepared inputs for assertions.
        public IList<RagEmbeddingInput> Inputs { get; } = new List<RagEmbeddingInput>();

        public async Task<RagEmbeddingInput> PrepareAsync(
            RagChunk chunk,
            CancellationToken cancellationToken = default)
        {
            // Delegating to the default preparer keeps the observed inputs aligned with production metadata-copy behavior.
            var input = await inner.PrepareAsync(chunk, cancellationToken);

            Inputs.Add(input);

            return input;
        }
    }

    private sealed class DeterministicTrackingEmbeddingProvider : IRagEmbeddingProvider
    {
        // The fake provider records provider-bound text and returns vectors derived only from that text.
        public IList<string> Texts { get; } = new List<string>();

        public Task<RagEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            // No network or SDK call is made; the generated vector is deterministic for the provider input text.
            Texts.Add(text);

            return Task.FromResult(new RagEmbedding(CreateEmbeddingValues(text)));
        }

        public static IReadOnlyList<float> CreateEmbeddingValues(string text)
        {
            // The checksum makes each vector content-sensitive while remaining stable in CI.
            var checksum = text.Sum(character => character);

            return [text.Length, checksum, text.Length == 0 ? 0 : text[0]];
        }
    }
}

