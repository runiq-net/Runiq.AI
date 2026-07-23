using Runiq.AI.Agents.Configuration;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Agents.Runtime;

internal static class RagResultAcceptanceEvaluator
{
    public static RagResultAcceptanceEvaluation Evaluate(
        IReadOnlyList<RagSearchResult> candidates,
        RagResultAcceptanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(options);

        var normalized = candidates.Select(Normalize).ToArray();
        if (normalized.All(result => result?.Provenance?.Mode is not (RagRetrievalMode.Lexical or RagRetrievalMode.Hybrid)))
        {
            Array.Sort(normalized, RagSearchResultComparer.Instance);
        }
        var accepted = new List<RagSearchResult>();
        var rejected = new List<RagRejectedResult>();
        var seenContent = new HashSet<string>(StringComparer.Ordinal);
        var seenChunks = new HashSet<(string DocumentId, string ChunkId)>();

        foreach (var candidate in normalized)
        {
            if (!TryValidateScore(candidate))
            {
                rejected.Add(new RagRejectedResult(candidate, RagResultRejectionReason.InvalidScore));
                continue;
            }

            if (candidate.Relevance is double relevance)
            {
                if (options.MinimumRelevance is double minimum && relevance < minimum)
                {
                    rejected.Add(new RagRejectedResult(candidate, RagResultRejectionReason.BelowMinimumRelevance));
                    continue;
                }
            }
            else if (candidate.Provenance?.LexicalRank is not null)
            {
                // Lexical and fusion ranks are valid signals but are intentionally not semantic relevance.
            }
            else if (options.ProviderSpecificAcceptance is null)
            {
                rejected.Add(new RagRejectedResult(candidate, RagResultRejectionReason.UnsupportedScoreMetric));
                continue;
            }
            else if (!options.ProviderSpecificAcceptance(candidate))
            {
                rejected.Add(new RagRejectedResult(candidate, RagResultRejectionReason.BelowMinimumRelevance));
                continue;
            }

            var normalizedContent = candidate.Chunk.Content.Trim();
            var chunkIdentity = (candidate.Chunk.DocumentId, candidate.Chunk.Id);
            if (seenChunks.Contains(chunkIdentity) || seenContent.Contains(normalizedContent))
            {
                rejected.Add(new RagRejectedResult(candidate, RagResultRejectionReason.DuplicateContent));
                continue;
            }

            if (accepted.Count >= options.MaximumAcceptedResults)
            {
                rejected.Add(new RagRejectedResult(candidate, RagResultRejectionReason.ResultLimitExceeded));
                continue;
            }

            accepted.Add(candidate);
            seenChunks.Add(chunkIdentity);
            seenContent.Add(normalizedContent);
        }

        return new RagResultAcceptanceEvaluation(normalized, accepted.ToArray(), rejected.ToArray());
    }

    private static RagSearchResult Normalize(RagSearchResult candidate)
    {
        if (candidate is null || candidate.RawScore is not double rawScore ||
            !double.IsFinite(rawScore) || string.IsNullOrWhiteSpace(candidate.Metric))
        {
            return candidate!;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(candidate.Metric, RagScoreMetrics.CosineSimilarity))
        {
            return candidate with
            {
                Relevance = candidate.HigherIsBetter == true && rawScore is >= -1.0 and <= 1.0
                    ? (rawScore + 1.0) / 2.0
                    : double.NaN,
            };
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(candidate.Metric, RagScoreMetrics.EuclideanDistance))
        {
            return candidate with
            {
                Relevance = candidate.HigherIsBetter == false && rawScore >= 0.0
                    ? 1.0 / (1.0 + rawScore)
                    : double.NaN,
            };
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(candidate.Metric, RagScoreMetrics.DotProduct))
        {
            return candidate with { Relevance = candidate.HigherIsBetter == true ? null : double.NaN };
        }

        return candidate;
    }

    private static bool TryValidateScore(RagSearchResult candidate)
    {
        if (candidate is null) return false;
        var hasLexicalContribution = candidate.Provenance?.LexicalRank is not null;
        var hasCoherentSemanticScore = candidate.RawScore is double rawScore &&
            double.IsFinite(rawScore) && !string.IsNullOrWhiteSpace(candidate.Metric);
        return (hasLexicalContribution || hasCoherentSemanticScore) &&
            (candidate.Relevance is null ||
                (double.IsFinite(candidate.Relevance.Value) && candidate.Relevance.Value is >= 0.0 and <= 1.0));
    }

    private sealed class RagSearchResultComparer : IComparer<RagSearchResult>
    {
        public static RagSearchResultComparer Instance { get; } = new();

        public int Compare(RagSearchResult? left, RagSearchResult? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return 1;
            if (right is null) return -1;

            var comparison = CompareScore(left, right);
            if (comparison != 0) return comparison;

            comparison = StringComparer.Ordinal.Compare(left.Chunk.DocumentId, right.Chunk.DocumentId);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.Chunk.Id, right.Chunk.Id);
        }

        private static int CompareScore(RagSearchResult left, RagSearchResult right)
        {
            if (left.Relevance.HasValue && right.Relevance.HasValue)
            {
                return right.Relevance.Value.CompareTo(left.Relevance.Value);
            }

            if (left.Relevance.HasValue) return -1;
            if (right.Relevance.HasValue) return 1;

            if (left.HigherIsBetter == right.HigherIsBetter && left.HigherIsBetter.HasValue)
            {
                return left.HigherIsBetter.Value
                    ? Nullable.Compare(right.RawScore, left.RawScore)
                    : Nullable.Compare(left.RawScore, right.RawScore);
            }

            return 0;
        }
    }
}

internal sealed record RagResultAcceptanceEvaluation(
    IReadOnlyList<RagSearchResult> Candidates,
    IReadOnlyList<RagSearchResult> AcceptedResults,
    IReadOnlyList<RagRejectedResult> RejectedResults);
