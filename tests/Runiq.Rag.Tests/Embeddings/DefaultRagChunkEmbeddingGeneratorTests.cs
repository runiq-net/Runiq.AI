using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Embeddings;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Tests.Embeddings;

public sealed class DefaultRagChunkEmbeddingGeneratorTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenEmbeddingProviderIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRagChunkEmbeddingGenerator(null!, new DefaultRagEmbeddingInputPreparer()));

        Assert.Equal("embeddingProvider", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenInputPreparerIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new DefaultRagChunkEmbeddingGenerator(new TrackingEmbeddingProvider(), null!));

        Assert.Equal("inputPreparer", exception.ParamName);
    }

    [Fact]
    public async Task GenerateAsync_ShouldCreateEmbeddingForEveryChunkInOrder()
    {
        var chunks = new[]
        {
            CreateChunk("chunk-1", "document-1", 0, "First chunk."),
            CreateChunk("chunk-2", "document-1", 1, "Second chunk."),
            CreateChunk("chunk-3", "document-2", 0, "Third chunk."),
        };
        var provider = new TrackingEmbeddingProvider(
            new RagEmbedding([1.0f]),
            new RagEmbedding([2.0f]),
            new RagEmbedding([3.0f]));
        var generator = new DefaultRagChunkEmbeddingGenerator(provider, new DefaultRagEmbeddingInputPreparer());

        var results = await generator.GenerateAsync(chunks);

        Assert.Collection(
            results,
            result =>
            {
                Assert.Equal("chunk-1", result.ChunkId);
                Assert.Equal("document-1", result.DocumentId);
                Assert.Equal(0, result.ChunkIndex);
                Assert.Equal([1.0f], result.Embedding.Values);
            },
            result =>
            {
                Assert.Equal("chunk-2", result.ChunkId);
                Assert.Equal("document-1", result.DocumentId);
                Assert.Equal(1, result.ChunkIndex);
                Assert.Equal([2.0f], result.Embedding.Values);
            },
            result =>
            {
                Assert.Equal("chunk-3", result.ChunkId);
                Assert.Equal("document-2", result.DocumentId);
                Assert.Equal(0, result.ChunkIndex);
                Assert.Equal([3.0f], result.Embedding.Values);
            });
    }

    [Fact]
    public async Task GenerateAsync_ShouldCallProviderWithPreparedTextForEveryChunk()
    {
        var chunks = new[]
        {
            CreateChunk("chunk-1", "document-1", 0, " raw first "),
            CreateChunk("chunk-2", "document-1", 1, " raw second "),
        };
        var provider = new TrackingEmbeddingProvider(new RagEmbedding([1.0f]), new RagEmbedding([2.0f]));
        var preparer = new PrefixingEmbeddingInputPreparer("prepared:");
        var generator = new DefaultRagChunkEmbeddingGenerator(provider, preparer);

        await generator.GenerateAsync(chunks);

        Assert.Equal(["prepared: raw first ", "prepared: raw second "], provider.Texts);
        Assert.Equal(["chunk-1", "chunk-2"], preparer.PreparedChunkIds);
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnEmptyResultsAndNotCallProvider_WhenChunksAreEmpty()
    {
        var provider = new TrackingEmbeddingProvider();
        var preparer = new TrackingEmbeddingInputPreparer();
        var generator = new DefaultRagChunkEmbeddingGenerator(provider, preparer);

        var results = await generator.GenerateAsync(Array.Empty<RagChunk>());

        Assert.Empty(results);
        Assert.Empty(provider.Texts);
        Assert.Empty(preparer.PreparedChunkIds);
    }

    [Fact]
    public async Task GenerateAsync_ShouldThrowForNullChunkList()
    {
        var generator = new DefaultRagChunkEmbeddingGenerator(
            new TrackingEmbeddingProvider(),
            new DefaultRagEmbeddingInputPreparer());

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => generator.GenerateAsync(null!));

        Assert.Equal("chunks", exception.ParamName);
    }

    [Fact]
    public async Task GenerateAsync_ShouldThrowDeterministicFailure_WhenChunkIsNull()
    {
        var chunks = new RagChunk?[] { CreateChunk("chunk-1", "document-1", 0, "First chunk."), null };
        var generator = new DefaultRagChunkEmbeddingGenerator(
            new TrackingEmbeddingProvider(new RagEmbedding([1.0f])),
            new DefaultRagEmbeddingInputPreparer());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            generator.GenerateAsync(chunks!));

        Assert.Equal("Chunk embedding generation failed at input index 1 because the chunk is null.", exception.Message);
    }

    [Fact]
    public async Task GenerateAsync_ShouldThrowDeterministicFailureWithChunkContext_WhenProviderFails()
    {
        var chunks = new[]
        {
            CreateChunk("chunk-1", "document-1", 0, "First chunk."),
            CreateChunk("chunk-2", "document-1", 1, "Second chunk."),
        };
        var provider = new TrackingEmbeddingProvider(new RagEmbedding([1.0f]))
        {
            FailureText = "Second chunk.",
        };
        var generator = new DefaultRagChunkEmbeddingGenerator(provider, new DefaultRagEmbeddingInputPreparer());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GenerateAsync(chunks));

        Assert.Equal(
            "Chunk embedding generation failed for chunk 'chunk-2' in document 'document-1' at input index 1.",
            exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(["First chunk.", "Second chunk."], provider.Texts);
    }

    [Fact]
    public async Task GenerateAsync_ShouldPassCancellationTokenToProvider()
    {
        var chunk = CreateChunk("chunk-1", "document-1", 0, "First chunk.");
        var provider = new TrackingEmbeddingProvider(new RagEmbedding([1.0f]));
        var generator = new DefaultRagChunkEmbeddingGenerator(provider, new DefaultRagEmbeddingInputPreparer());
        using var cancellationTokenSource = new CancellationTokenSource();

        await generator.GenerateAsync([chunk], cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, provider.CancellationTokens.Single());
    }

    [Fact]
    public async Task GenerateAsync_ShouldPassCancellationTokenToInputPreparer()
    {
        var chunk = CreateChunk("chunk-1", "document-1", 0, "First chunk.");
        var preparer = new TrackingEmbeddingInputPreparer();
        var generator = new DefaultRagChunkEmbeddingGenerator(
            new TrackingEmbeddingProvider(new RagEmbedding([1.0f])),
            preparer);
        using var cancellationTokenSource = new CancellationTokenSource();

        await generator.GenerateAsync([chunk], cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, preparer.CancellationTokens.Single());
    }

    [Fact]
    public async Task GenerateAsync_ShouldPropagateCancellationWithoutWrapping()
    {
        var chunk = CreateChunk("chunk-1", "document-1", 0, "First chunk.");
        var provider = new TrackingEmbeddingProvider(new RagEmbedding([1.0f]))
        {
            CancelOnGenerate = true,
        };
        var generator = new DefaultRagChunkEmbeddingGenerator(provider, new DefaultRagEmbeddingInputPreparer());

        await Assert.ThrowsAsync<OperationCanceledException>(() => generator.GenerateAsync([chunk]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAsync_ShouldPassPreparedEmptyOrWhitespaceContentToProvider(string content)
    {
        var chunk = CreateChunk("chunk-1", "document-1", 0, content);
        var provider = new TrackingEmbeddingProvider(new RagEmbedding([1.0f]));
        var preparer = new TrackingEmbeddingInputPreparer();
        var generator = new DefaultRagChunkEmbeddingGenerator(provider, preparer);

        await generator.GenerateAsync([chunk]);

        Assert.Equal(["chunk-1"], preparer.PreparedChunkIds);
        Assert.Equal([content], provider.Texts);
    }

    [Fact]
    public async Task GenerateAsync_ShouldPreservePreparerMetadataMapping()
    {
        var chunk = CreateChunk("chunk-1", "document-1", 7, "First chunk.");
        var provider = new TrackingEmbeddingProvider(new RagEmbedding([1.0f]));
        var preparer = new MetadataAwareEmbeddingInputPreparer();
        var generator = new DefaultRagChunkEmbeddingGenerator(provider, preparer);

        var result = Assert.Single(await generator.GenerateAsync([chunk]));

        Assert.Equal("chunk-1:10:42", provider.Texts.Single());
        Assert.Equal("chunk-1", result.ChunkId);
        Assert.Equal("document-1", result.DocumentId);
        Assert.Equal(7, result.ChunkIndex);
    }

    [Fact]
    public void IRagEmbeddingProvider_ShouldKeepSingleTextGenerateContract()
    {
        var method = Assert.Single(
            typeof(IRagEmbeddingProvider).GetMethods(),
            method => method.Name == nameof(IRagEmbeddingProvider.GenerateAsync));
        var parameters = method.GetParameters();

        Assert.Equal(typeof(Task<RagEmbedding>), method.ReturnType);
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }

    private static RagChunk CreateChunk(
        string id,
        string documentId,
        int index,
        string content)
    {
        return new RagChunk
        {
            Id = id,
            DocumentId = documentId,
            Index = index,
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

    private sealed class TrackingEmbeddingProvider : IRagEmbeddingProvider
    {
        private readonly Queue<RagEmbedding> embeddings;

        public TrackingEmbeddingProvider(params RagEmbedding[] embeddings)
        {
            this.embeddings = new Queue<RagEmbedding>(embeddings);
        }

        public IList<string> Texts { get; } = new List<string>();

        public IList<CancellationToken> CancellationTokens { get; } = new List<CancellationToken>();

        public string? FailureText { get; init; }

        public bool CancelOnGenerate { get; init; }

        public Task<RagEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            Texts.Add(text);
            CancellationTokens.Add(cancellationToken);

            if (CancelOnGenerate)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (text == FailureText)
            {
                throw new InvalidOperationException("Provider failed.");
            }

            return Task.FromResult(embeddings.Count > 0 ? embeddings.Dequeue() : new RagEmbedding());
        }
    }

    private class TrackingEmbeddingInputPreparer : IRagEmbeddingInputPreparer
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

    private sealed class PrefixingEmbeddingInputPreparer : TrackingEmbeddingInputPreparer
    {
        private readonly string prefix;

        public PrefixingEmbeddingInputPreparer(string prefix)
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

    private sealed class MetadataAwareEmbeddingInputPreparer : IRagEmbeddingInputPreparer
    {
        public Task<RagEmbeddingInput> PrepareAsync(
            RagChunk chunk,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RagEmbeddingInput
            {
                Id = chunk.Id,
                ChunkId = chunk.Id,
                DocumentId = chunk.DocumentId,
                Content = $"{chunk.Id}:{chunk.Metadata.StartIndex}:{chunk.Metadata.EndIndex}",
                ChunkIndex = chunk.Index,
                Metadata = chunk.Metadata,
            });
        }
    }
}
