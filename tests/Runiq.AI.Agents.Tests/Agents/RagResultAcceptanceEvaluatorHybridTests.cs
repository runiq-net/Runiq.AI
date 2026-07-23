using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class RagResultAcceptanceEvaluatorHybridTests
{
    [Fact]
    // Verifies hybrid RRF order remains authoritative when semantic scores would imply another order.
    public void Evaluate_ShouldPreserveHybridRrfOrder()
    {
        var evaluation = RagResultAcceptanceEvaluator.Evaluate(
            [Create("rrf-first", 0.1, 1, 2), Create("semantic-first", 0.9, 2, 1)],
            new RagResultAcceptanceOptions { MaximumAcceptedResults = 5 });

        Assert.Equal(["rrf-first", "semantic-first"], evaluation.AcceptedResults.Select(x => x.Chunk.Id));
    }

    [Fact]
    // Verifies rejection removes a hybrid candidate without reordering the remaining fused candidates.
    public void Evaluate_ShouldRemoveRejectedHybridCandidateWithoutReordering()
    {
        var evaluation = RagResultAcceptanceEvaluator.Evaluate(
            [Create("first", 0.9, 1, 1), Create("rejected", 0.1, 2, null), Create("third", 0.8, 3, 2)],
            new RagResultAcceptanceOptions { MinimumRelevance = 0.5, MaximumAcceptedResults = 5 });

        Assert.Equal(["first", "third"], evaluation.AcceptedResults.Select(x => x.Chunk.Id));
        Assert.Equal(RagResultRejectionReason.BelowMinimumRelevance, Assert.Single(evaluation.RejectedResults).Reason);
    }

    [Fact]
    // Verifies a semantic-only hybrid candidate without normalized relevance still requires provider-specific acceptance.
    public void Evaluate_ShouldRejectSemanticOnlyHybridCandidateWithoutNormalizedRelevance()
    {
        var candidate = Create("semantic-only", null, 1, null) with
        {
            Metric = RagScoreMetrics.DotProduct,
            RawScore = 3.0,
        };

        var evaluation = RagResultAcceptanceEvaluator.Evaluate(
            [candidate], new RagResultAcceptanceOptions { MaximumAcceptedResults = 5 });

        Assert.Empty(evaluation.AcceptedResults);
        Assert.Equal(RagResultRejectionReason.UnsupportedScoreMetric, Assert.Single(evaluation.RejectedResults).Reason);
    }

    [Fact]
    // Verifies lexical contribution permits missing semantic relevance for lexical-only and combined hybrid candidates.
    public void Evaluate_ShouldAcceptLexicallyContributedHybridCandidatesWithoutSemanticRelevance()
    {
        var lexicalOnly = Create("lexical-only", null, null, 1) with
        {
            RawScore = null,
            Metric = null,
            HigherIsBetter = false,
        };
        var combined = Create("combined", null, 1, 2) with { Metric = RagScoreMetrics.DotProduct };

        var evaluation = RagResultAcceptanceEvaluator.Evaluate(
            [lexicalOnly, combined], new RagResultAcceptanceOptions { MaximumAcceptedResults = 5 });

        Assert.Equal(2, evaluation.AcceptedResults.Count);
        Assert.Empty(evaluation.RejectedResults);
    }

    private static RagSearchResult Create(string id, double? relevance, int? semanticRank, int? lexicalRank) => new()
    {
        Chunk = new RagChunk { Id = id, DocumentId = "document", Content = id },
        RawScore = relevance is double value ? value * 2 - 1 : 0.5,
        Relevance = relevance,
        Metric = RagScoreMetrics.CosineSimilarity,
        HigherIsBetter = true,
        Provenance = new RagRetrievalProvenance
        {
            Mode = RagRetrievalMode.Hybrid,
            SemanticRank = semanticRank,
            LexicalRank = lexicalRank,
            FusedRank = lexicalRank ?? semanticRank,
        },
    };
}
