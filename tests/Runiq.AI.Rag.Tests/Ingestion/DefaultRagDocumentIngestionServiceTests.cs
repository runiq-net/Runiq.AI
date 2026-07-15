using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Rag.Abstractions.Chunking;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Embeddings;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.Services;

namespace Runiq.AI.Rag.Tests.Ingestion;

public sealed class DefaultRagDocumentIngestionServiceTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenChunkerIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRagDocumentIngestionService(null!, new TrackingChunkEmbeddingGenerator()));

        Assert.Equal("chunker", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenChunkEmbeddingGeneratorIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRagDocumentIngestionService(new TrackingChunker(), null!));

        Assert.Equal("chunkEmbeddingGenerator", exception.ParamName);
    }

    [Fact]
    public async Task IngestAsync_ShouldStartIngestionFromRagDocumentAndUseChunker()
    {
        var document = CreateDocument("document-1", "First chunk. Second chunk.");
        var chunker = new TrackingChunker(CreateChunk("chunk-1", 0, "First chunk."));
        var generator = new TrackingChunkEmbeddingGenerator(CreateEmbeddingResult("chunk-1", 0, [1.0f]));
        var service = new DefaultRagDocumentIngestionService(chunker, generator);

        var result = await service.IngestAsync(document);

        Assert.Equal("document-1", result.DocumentId);
        Assert.Same(document, chunker.Documents.Single());
        Assert.Same(chunker.Chunks.Single(), result.Chunks.Single());
    }

    [Fact]
    public async Task IngestAsync_ShouldPreserveChunkOrderMetadataAndEmbeddingAssociation()
    {
        var metadata1 = CreateChunkMetadata("intro");
        var metadata2 = CreateChunkMetadata("details");
        var chunks = new[]
        {
            CreateChunk("chunk-1", 0, "First chunk.", metadata1),
            CreateChunk("chunk-2", 1, "Second chunk.", metadata2),
        };
        var chunker = new TrackingChunker(chunks);
        var generator = new TrackingChunkEmbeddingGenerator(
            CreateEmbeddingResult("chunk-1", 0, [1.0f]),
            CreateEmbeddingResult("chunk-2", 1, [2.0f]));
        var service = new DefaultRagDocumentIngestionService(chunker, generator);

        var result = await service.IngestAsync(CreateDocument("document-1", "content"));

        Assert.Equal(["chunk-1", "chunk-2"], result.Chunks.Select(chunk => chunk.Id));
        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Same(chunks[0], item.Chunk);
                Assert.Same(metadata1, item.Chunk.Metadata);
                Assert.Equal("chunk-1", item.EmbeddingResult.ChunkId);
                Assert.Equal([1.0f], item.EmbeddingResult.Embedding.Values);
            },
            item =>
            {
                Assert.Same(chunks[1], item.Chunk);
                Assert.Same(metadata2, item.Chunk.Metadata);
                Assert.Equal("chunk-2", item.EmbeddingResult.ChunkId);
                Assert.Equal([2.0f], item.EmbeddingResult.Embedding.Values);
            });
    }

    [Fact]
    public async Task IngestAsync_ShouldUseChunkContentThroughInputPreparerAndProvider()
    {
        var chunks = new[]
        {
            CreateChunk("chunk-1", 0, " raw first "),
            CreateChunk("chunk-2", 1, " raw second "),
        };
        var chunker = new TrackingChunker(chunks);
        var preparer = new PrefixingInputPreparer("prepared:");
        var embeddingClient = new TrackingEmbeddingClient([1.0f], [2.0f]);
        var generator = new DefaultRagChunkEmbeddingGenerator(embeddingClient, preparer);
        var service = new DefaultRagDocumentIngestionService(chunker, generator);

        await service.IngestAsync(CreateDocument("document-1", "content"));

        Assert.Equal(["chunk-1", "chunk-2"], preparer.PreparedChunkIds);
        Assert.Equal(["prepared: raw first ", "prepared: raw second "], embeddingClient.Texts);
        Assert.Equal(1, embeddingClient.InvocationCount);
    }

    [Fact]
    public async Task IngestAsync_ShouldGenerateOneEmbeddingForEachChunkWithFakeProvider()
    {
        var chunks = new[]
        {
            CreateChunk("chunk-1", 0, "First chunk."),
            CreateChunk("chunk-2", 1, "Second chunk."),
            CreateChunk("chunk-3", 2, "Third chunk."),
        };
        var embeddingClient = new TrackingEmbeddingClient([1.0f], [2.0f], [3.0f]);
        var service = new DefaultRagDocumentIngestionService(
            new TrackingChunker(chunks),
            new DefaultRagChunkEmbeddingGenerator(embeddingClient, new TrackingInputPreparer()));

        var result = await service.IngestAsync(CreateDocument("document-1", "content"));

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(["First chunk.", "Second chunk.", "Third chunk."], embeddingClient.Texts);
        Assert.Equal(1, embeddingClient.InvocationCount);
        Assert.Equal(["chunk-1", "chunk-2", "chunk-3"], result.Items.Select(item => item.EmbeddingResult.ChunkId));
    }

    [Fact]
    public async Task IngestAsync_ShouldReturnDeterministicEmptyResult_WhenDocumentContentProducesNoChunks()
    {
        var chunker = new TrackingChunker();
        var generator = new TrackingChunkEmbeddingGenerator();
        var service = new DefaultRagDocumentIngestionService(chunker, generator);

        var result = await service.IngestAsync(CreateDocument("document-1", string.Empty));

        Assert.Equal("document-1", result.DocumentId);
        Assert.Empty(result.Chunks);
        Assert.Empty(result.Items);
        Assert.Empty(generator.Chunks);
    }

    [Fact]
    public async Task IngestAsync_ShouldThrowDeterministicFailure_WhenChunkerFails()
    {
        var chunker = new TrackingChunker
        {
            Failure = new InvalidOperationException("Chunker failed."),
        };
        var service = new DefaultRagDocumentIngestionService(chunker, new TrackingChunkEmbeddingGenerator());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestAsync(CreateDocument("document-1", "content")));

        Assert.Equal("RAG document ingestion failed while chunking document 'document-1'.", exception.Message);
        Assert.Equal("Chunker failed.", exception.InnerException?.Message);
    }

    [Fact]
    public async Task IngestAsync_ShouldThrowDeterministicFailure_WhenEmbeddingProviderFails()
    {
        var chunks = new[]
        {
            CreateChunk("chunk-1", 0, "First chunk."),
            CreateChunk("chunk-2", 1, "Second chunk."),
        };
        var embeddingClient = new TrackingEmbeddingClient([1.0f])
        {
            FailureText = "Second chunk.",
        };
        var service = new DefaultRagDocumentIngestionService(
            new TrackingChunker(chunks),
            new DefaultRagChunkEmbeddingGenerator(embeddingClient, new TrackingInputPreparer()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestAsync(CreateDocument("document-1", "content")));

        Assert.Equal(
            "RAG document ingestion failed while generating chunk embeddings for document 'document-1'.",
            exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(["First chunk.", "Second chunk."], embeddingClient.Texts);
        Assert.Equal(1, embeddingClient.InvocationCount);
    }

    [Fact]
    public async Task IngestAsync_ShouldPassCancellationTokenThroughChunkerInputPreparerAndProvider()
    {
        var chunk = CreateChunk("chunk-1", 0, "First chunk.");
        var chunker = new TrackingChunker(chunk);
        var preparer = new TrackingInputPreparer();
        var embeddingClient = new TrackingEmbeddingClient([1.0f]);
        var service = new DefaultRagDocumentIngestionService(
            chunker,
            new DefaultRagChunkEmbeddingGenerator(embeddingClient, preparer));
        using var cancellationTokenSource = new CancellationTokenSource();

        await service.IngestAsync(CreateDocument("document-1", "content"), cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, chunker.CancellationTokens.Single());
        Assert.Equal(cancellationTokenSource.Token, preparer.CancellationTokens.Single());
        Assert.Equal(cancellationTokenSource.Token, embeddingClient.CancellationTokens.Single());
    }

    [Fact]
    public async Task IngestAsync_ShouldPropagateCancellationWithoutWrapping()
    {
        var chunker = new TrackingChunker
        {
            CancelOnChunk = true,
        };
        var service = new DefaultRagDocumentIngestionService(chunker, new TrackingChunkEmbeddingGenerator());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.IngestAsync(CreateDocument("document-1", "content")));
    }

    [Fact]
    public async Task IngestAsync_ShouldThrow_WhenEmbeddingResultDoesNotMatchChunk()
    {
        var service = new DefaultRagDocumentIngestionService(
            new TrackingChunker(CreateChunk("chunk-1", 0, "First chunk.")),
            new TrackingChunkEmbeddingGenerator(CreateEmbeddingResult("chunk-2", 0, [1.0f])));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IngestAsync(CreateDocument("document-1", "content")));

        Assert.Equal(
            "RAG document ingestion expected embedding result for chunk 'chunk-1' at index 0 but received 'chunk-2'.",
            exception.Message);
    }

    [Fact]
    public async Task IngestAsync_ShouldNotCallVectorStoreUpsert()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRagVectorStore, ThrowingVectorStore>();
        services.AddSingleton<IRagChunker>(_ => new TrackingChunker(CreateChunk("chunk-1", 0, "First chunk.")));
        var embeddingClient = new TrackingEmbeddingClient([1.0f]);
        services.AddSingleton<IEmbeddingClient>(embeddingClient);
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetRequiredService<IRagDocumentIngestionService>();

        var result = await service.IngestAsync(CreateDocument("document-1", "content"));

        Assert.Single(result.Items);
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveDocumentIngestionService()
    {
        var services = new ServiceCollection();
        var embeddingClient = new TrackingEmbeddingClient([1.0f]);

        services.AddSingleton<IEmbeddingClient>(embeddingClient);
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<DefaultRagDocumentIngestionService>(
            serviceProvider.GetRequiredService<IRagDocumentIngestionService>());
    }

    private static RagDocument CreateDocument(string id, string content)
    {
        return new RagDocument
        {
            Id = id,
            Content = content,
            Metadata = new RagDocumentMetadata
            {
                SourceName = "source.md",
            },
        };
    }

    private static RagChunk CreateChunk(
        string id,
        int index,
        string content,
        RagChunkMetadata? metadata = null)
    {
        return new RagChunk
        {
            Id = id,
            DocumentId = "document-1",
            Index = index,
            Content = content,
            Metadata = metadata ?? CreateChunkMetadata("default"),
        };
    }

    private static RagChunkMetadata CreateChunkMetadata(string section)
    {
        return new RagChunkMetadata
        {
            StartIndex = 10,
            EndIndex = 42,
            TokenCount = 8,
            AdditionalMetadata = new RagMetadata(new Dictionary<string, string>
            {
                ["section"] = section,
            }),
        };
    }

    private static RagChunkEmbeddingResult CreateEmbeddingResult(
        string chunkId,
        int chunkIndex,
        IReadOnlyList<float> values)
    {
        return new RagChunkEmbeddingResult
        {
            ChunkId = chunkId,
            DocumentId = "document-1",
            ChunkIndex = chunkIndex,
            Embedding = new RagEmbedding(values),
        };
    }

    private sealed class TrackingChunker : IRagChunker
    {
        private readonly IReadOnlyList<RagChunk> chunks;

        public TrackingChunker(params RagChunk[] chunks)
        {
            this.chunks = chunks;
        }

        public IList<RagDocument> Documents { get; } = new List<RagDocument>();

        public IList<CancellationToken> CancellationTokens { get; } = new List<CancellationToken>();

        public IReadOnlyList<RagChunk> Chunks => chunks;

        public Exception? Failure { get; init; }

        public bool CancelOnChunk { get; init; }

        public Task<IReadOnlyList<RagChunk>> ChunkAsync(
            RagDocument document,
            CancellationToken cancellationToken = default)
        {
            Documents.Add(document);
            CancellationTokens.Add(cancellationToken);

            if (CancelOnChunk)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(chunks);
        }
    }

    private sealed class TrackingChunkEmbeddingGenerator : IRagChunkEmbeddingGenerator
    {
        private readonly IReadOnlyList<RagChunkEmbeddingResult> embeddingResults;

        public TrackingChunkEmbeddingGenerator(params RagChunkEmbeddingResult[] embeddingResults)
        {
            this.embeddingResults = embeddingResults;
        }

        public IList<IReadOnlyList<RagChunk>> Chunks { get; } = new List<IReadOnlyList<RagChunk>>();

        public Task<IReadOnlyList<RagChunkEmbeddingResult>> GenerateAsync(
            IReadOnlyList<RagChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            Chunks.Add(chunks);

            return Task.FromResult(embeddingResults);
        }
    }

    private class TrackingInputPreparer : IRagEmbeddingInputPreparer
    {
        public IList<string> PreparedChunkIds { get; } = new List<string>();

        public IList<CancellationToken> CancellationTokens { get; } = new List<CancellationToken>();

        public virtual Task<RagEmbeddingInput> PrepareAsync(
            RagChunk chunk,
            CancellationToken cancellationToken = default)
        {
            PreparedChunkIds.Add(chunk.Id);
            CancellationTokens.Add(cancellationToken);

            return Task.FromResult(new RagEmbeddingInput
            {
                Id = chunk.Id,
                ChunkId = chunk.Id,
                DocumentId = chunk.DocumentId,
                Content = chunk.Content,
                ChunkIndex = chunk.Index,
                Metadata = chunk.Metadata,
            });
        }
    }

    private sealed class PrefixingInputPreparer : TrackingInputPreparer
    {
        private readonly string prefix;

        public PrefixingInputPreparer(string prefix)
        {
            this.prefix = prefix;
        }

        public override Task<RagEmbeddingInput> PrepareAsync(
            RagChunk chunk,
            CancellationToken cancellationToken = default)
        {
            PreparedChunkIds.Add(chunk.Id);
            CancellationTokens.Add(cancellationToken);

            return Task.FromResult(new RagEmbeddingInput
            {
                Id = chunk.Id,
                ChunkId = chunk.Id,
                DocumentId = chunk.DocumentId,
                Content = prefix + chunk.Content,
                ChunkIndex = chunk.Index,
                Metadata = chunk.Metadata,
            });
        }
    }

    private sealed class TrackingEmbeddingClient : IEmbeddingClient
    {
        private readonly Queue<IReadOnlyList<float>> vectors;

        public TrackingEmbeddingClient(params IReadOnlyList<float>[] vectors)
        {
            this.vectors = new Queue<IReadOnlyList<float>>(vectors);
        }

        public IList<string> Texts { get; } = new List<string>();

        public IList<CancellationToken> CancellationTokens { get; } = new List<CancellationToken>();

        public int InvocationCount { get; private set; }

        public string? FailureText { get; init; }

        public Task<EmbeddingResponse> EmbedAsync(
            EmbeddingRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            InvocationCount++;
            foreach (var input in request.Inputs)
            {
                Texts.Add(input);
            }

            CancellationTokens.Add(cancellationToken);

            if (FailureText is not null && request.Inputs.Contains(FailureText, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Provider failed.");
            }

            var results = request.Inputs.Select((_, index) =>
            {
                var vector = vectors.Count > 0 ? vectors.Dequeue() : Array.Empty<float>();
                return new EmbeddingResult(index, vector, vector.Count);
            }).ToList();

            return Task.FromResult(new EmbeddingResponse(results));
        }
    }

    private sealed class ThrowingVectorStore : IRagVectorStore
    {
        public Task<CreateVectorIndexResult> CreateIndexAsync(
            CreateVectorIndexRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Vector store create should not be called.");
        }

        public Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Vector store upsert should not be called.");
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Vector store search should not be called.");
        }
    }
}

