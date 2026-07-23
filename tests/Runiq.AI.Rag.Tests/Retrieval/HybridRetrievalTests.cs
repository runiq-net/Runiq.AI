using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Retrieval;
using Runiq.AI.Rag.Tests.TestDoubles;
using Runiq.AI.Rag.VectorStores.InMemory;

namespace Runiq.AI.Rag.Tests.Retrieval;

public sealed class HybridRetrievalTests
{
    [Theory]
    [InlineData(RagRetrievalMode.Semantic, 1, 0)]
    [InlineData(RagRetrievalMode.Lexical, 0, 1)]
    [InlineData(RagRetrievalMode.Hybrid, 1, 1)]
    // Verifies each retrieval mode invokes only its required sources and hybrid invokes each source exactly once.
    public async Task RetrievalModes_ShouldInvokeRequiredSourcesExactlyOnce(
        RagRetrievalMode mode, int semanticCalls, int lexicalCalls)
    {
        var store = new RecordingSourceStore();
        var pipeline = new DefaultRagRetrievalPipeline(store);

        var result = await pipeline.RetrieveAsync(new RetrievalRequest
        {
            IndexName = "docs",
            QueryText = "query",
            QueryVector = [1f, 0f],
            Mode = mode,
        });

        Assert.True(result.Succeeded);
        Assert.Equal(semanticCalls, store.SemanticCalls);
        Assert.Equal(lexicalCalls, store.LexicalCalls);
        Assert.Equal(semanticCalls, result.Statistics.SemanticCandidateCount);
        Assert.Equal(lexicalCalls, result.Statistics.LexicalCandidateCount);
        Assert.Equal(mode == RagRetrievalMode.Hybrid ? 2 : 0, result.Statistics.FusedCandidateCount);
    }

    [Fact]
    // Verifies a semantic-source failure blocks hybrid retrieval before lexical fallback can run.
    public async Task HybridMode_ShouldStopAfterSemanticSourceFailure()
    {
        var store = new RecordingSourceStore { FailSemantic = true };
        var result = await new DefaultRagRetrievalPipeline(store).RetrieveAsync(HybridRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(1, store.SemanticCalls);
        Assert.Equal(0, store.LexicalCalls);
    }

    [Fact]
    // Verifies a lexical-source failure blocks hybrid retrieval without returning semantic-only results.
    public async Task HybridMode_ShouldFailAfterLexicalSourceFailureWithoutFallback()
    {
        var store = new RecordingSourceStore { FailLexical = true };
        var result = await new DefaultRagRetrievalPipeline(store).RetrieveAsync(HybridRequest());

        Assert.False(result.Succeeded);
        Assert.Empty(result.Items);
        Assert.Equal(1, store.SemanticCalls);
        Assert.Equal(1, store.LexicalCalls);
    }

    [Fact]
    // Verifies hybrid retrieval propagates cancellation from the lexical source.
    public async Task HybridMode_ShouldPropagateSourceCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new RecordingSourceStore { CancelLexical = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DefaultRagRetrievalPipeline(store).RetrieveAsync(HybridRequest(), cancellation.Token));

        Assert.Equal(1, store.SemanticCalls);
        Assert.Equal(1, store.LexicalCalls);
    }

    [Fact]
    // Verifies final TopK limiting does not reduce authoritative source or pre-limit fused counts.
    public async Task HybridMode_ShouldPreservePreLimitCountsAfterTopK()
    {
        var result = await new DefaultRagRetrievalPipeline(new RecordingSourceStore()).RetrieveAsync(new()
        {
            IndexName = "docs",
            QueryText = "query",
            QueryVector = [1f, 0f],
            Mode = RagRetrievalMode.Hybrid,
            TopK = 1,
        });

        Assert.Single(result.Items);
        Assert.Equal(1, result.Statistics.SemanticCandidateCount);
        Assert.Equal(1, result.Statistics.LexicalCandidateCount);
        Assert.Equal(2, result.Statistics.FusedCandidateCount);
    }

    // Verifies that lexical retrieval preserves representative technical identifier forms without embeddings.
    [Theory]
    [InlineData("CS1503")]
    [InlineData("POL-HR-014")]
    [InlineData("IRagRetriever")]
    [InlineData("UseRag")]
    [InlineData("RagSearchCompleted")]
    [InlineData("filename.cs")]
    public async Task LexicalMode_ShouldRetrieveTechnicalIdentifiersWithoutEmbedding(string identifier)
    {
        var store = await CreateStoreAsync($"The reference {identifier} is documented here.");
        var pipeline = new DefaultRagRetrievalPipeline(store);

        var result = await pipeline.RetrieveAsync(new RetrievalRequest
        {
            IndexName = "docs",
            QueryText = identifier,
            Mode = RagRetrievalMode.Lexical,
        });

        Assert.True(result.Succeeded);
        Assert.Single(result.Items);
        Assert.Null(result.Items[0].RawScore);
        Assert.Null(result.Items[0].Relevance);
        Assert.Null(result.Items[0].Metric);
        Assert.Null(result.Items[0].HigherIsBetter);
        Assert.NotNull(result.Items[0].Provenance?.LexicalRawScore);
        Assert.Equal(1, result.Items[0].Provenance?.LexicalRank);
    }

    // Verifies that quoted text is treated as exact phrase intent by the provider query path.
    [Fact]
    public async Task LexicalMode_ShouldSupportQuotedExactPhrase()
    {
        var store = await CreateStoreAsync("hybrid retrieval preserves exact phrase order");
        var pipeline = new DefaultRagRetrievalPipeline(store);

        var match = await pipeline.RetrieveAsync(new RetrievalRequest
        {
            IndexName = "docs",
            QueryText = "\"exact phrase order\"",
            Mode = RagRetrievalMode.Lexical,
        });
        var miss = await pipeline.RetrieveAsync(new RetrievalRequest
        {
            IndexName = "docs",
            QueryText = "\"phrase exact order\"",
            Mode = RagRetrievalMode.Lexical,
        });

        Assert.Single(match.Items);
        Assert.Empty(miss.Items);
    }

    // Verifies that hybrid retrieval invokes embeddings once, merges a duplicate chunk, and applies one-based RRF.
    [Fact]
    public async Task HybridMode_ShouldFuseSemanticAndLexicalRanks()
    {
        var store = await CreateStoreAsync("CS1503 compiler error");
        var embeddings = new RecordingEmbeddingClient(2);
        var pipeline = new DefaultRagRetrievalPipeline(embeddings, store);

        var result = await pipeline.RetrieveAsync(new RetrievalRequest
        {
            IndexName = "docs",
            QueryText = "CS1503",
            Mode = RagRetrievalMode.Hybrid,
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(1, embeddings.InvocationCount);
        Assert.Equal(1, item.Provenance?.SemanticRank);
        Assert.Equal(1, item.Provenance?.LexicalRank);
        Assert.Equal(1, item.Provenance?.FusedRank);
        Assert.Equal(2d / 61d, item.Provenance!.ReciprocalRankFusionScore!.Value, 12);
        Assert.NotNull(item.RawScore);
        Assert.NotNull(item.Metric);
        Assert.NotNull(item.Provenance.LexicalRawScore);
        Assert.Equal(1, result.Statistics.SemanticCandidateCount);
        Assert.Equal(1, result.Statistics.LexicalCandidateCount);
        Assert.Equal(1, result.Statistics.FusedCandidateCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Verifies semantic score direction preserves both higher-is-better and lower-is-better provider meanings.
    public async Task SemanticMode_ShouldPreserveScoreDirection(bool higherIsBetter)
    {
        var store = new RecordingSourceStore { SemanticHigherIsBetter = higherIsBetter };
        var result = await new DefaultRagRetrievalPipeline(store).RetrieveAsync(new()
        {
            IndexName = "docs",
            QueryText = "query",
            QueryVector = [1f, 0f],
            Mode = RagRetrievalMode.Semantic,
        });

        Assert.Equal(higherIsBetter, Assert.Single(result.Items).HigherIsBetter);
    }

    private static async Task<InMemoryRagVectorStore> CreateStoreAsync(string content)
    {
        var store = new InMemoryRagVectorStore();
        await store.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "docs",
            Dimensions = 2,
        });
        await store.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "docs",
            Records =
            [
                new VectorRecord
                {
                    Id = "chunk-1",
                    Content = content,
                    Values = [0.1f, 0.2f],
                    Metadata = new RagMetadata(new Dictionary<string, string>
                    {
                        ["documentId"] = "document-1",
                    }),
                },
            ],
        });
        return store;
    }

    private static RetrievalRequest HybridRequest() => new()
    {
        IndexName = "docs",
        QueryText = "query",
        QueryVector = [1f, 0f],
        Mode = RagRetrievalMode.Hybrid,
    };

    private sealed class RecordingSourceStore : IRagVectorStore
    {
        public int SemanticCalls { get; private set; }
        public int LexicalCalls { get; private set; }
        public bool FailSemantic { get; init; }
        public bool FailLexical { get; init; }
        public bool CancelLexical { get; init; }
        public bool SemanticHigherIsBetter { get; init; } = true;

        public Task<CreateVectorIndexResult> CreateIndexAsync(
            CreateVectorIndexRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query, RagEmbedding embedding, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<QueryVectorResult> QueryAsync(
            QueryVectorRequest request, CancellationToken cancellationToken = default)
        {
            SemanticCalls++;
            if (FailSemantic) throw new InvalidOperationException("semantic failure");
            return Task.FromResult(new QueryVectorResult
            {
                Succeeded = true,
                Records =
                [
                    new VectorSearchResult
                    {
                        Id = "semantic",
                        Content = "semantic query",
                        RawScore = 0.8,
                        Relevance = 0.9,
                        Metric = RagScoreMetrics.CosineSimilarity,
                        HigherIsBetter = SemanticHigherIsBetter,
                    },
                ],
            });
        }

        public Task<QueryLexicalResult> QueryLexicalAsync(
            QueryLexicalRequest request, CancellationToken cancellationToken = default)
        {
            LexicalCalls++;
            if (CancelLexical) throw new OperationCanceledException(cancellationToken);
            if (FailLexical) throw new InvalidOperationException("lexical failure");
            return Task.FromResult(new QueryLexicalResult
            {
                Succeeded = true,
                Records =
                [
                    new VectorSearchResult
                    {
                        Id = "lexical",
                        Content = "lexical query",
                        RawScore = 1.0,
                    },
                ],
            });
        }
    }
}
