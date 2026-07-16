using Runiq.AI.Agents.Configuration;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Runtime;

internal static class RagResultAcceptanceEvaluator
{
    public static RagResultAcceptanceEvaluation Evaluate(
        IReadOnlyList<RagSearchResult> candidates,
        RagResultAcceptanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(options);

        var normalized = candidates
            .Select(Normalize)
            .OrderBy(result => result, RagSearchResultComparer.Instance)
            .ToArray();
        var accepted = new List<RagSearchResult>();
        var rejected = new List<RagRejectedResult>();
        var seenContent = new HashSet<string>(StringComparer.Ordinal);

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
            if (seenContent.Contains(normalizedContent))
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
            seenContent.Add(normalizedContent);
        }

        return new RagResultAcceptanceEvaluation(normalized, accepted.ToArray(), rejected.ToArray());
    }

    private static RagSearchResult Normalize(RagSearchResult candidate)
    {
        if (candidate is null || !double.IsFinite(candidate.RawScore) || string.IsNullOrWhiteSpace(candidate.Metric))
        {
            return candidate!;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(candidate.Metric, RagScoreMetrics.CosineSimilarity))
        {
            return candidate with
            {
                Relevance = candidate.HigherIsBetter && candidate.RawScore is >= -1.0 and <= 1.0
                    ? (candidate.RawScore + 1.0) / 2.0
                    : double.NaN,
            };
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(candidate.Metric, RagScoreMetrics.EuclideanDistance))
        {
            return candidate with
            {
                Relevance = !candidate.HigherIsBetter && candidate.RawScore >= 0.0
                    ? 1.0 / (1.0 + candidate.RawScore)
                    : double.NaN,
            };
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(candidate.Metric, RagScoreMetrics.DotProduct))
        {
            return candidate with { Relevance = candidate.HigherIsBetter ? null : double.NaN };
        }

        return candidate;
    }

    private static bool TryValidateScore(RagSearchResult candidate)
    {
        return candidate is not null &&
            double.IsFinite(candidate.RawScore) &&
            !string.IsNullOrWhiteSpace(candidate.Metric) &&
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

            if (left.HigherIsBetter == right.HigherIsBetter)
            {
                return left.HigherIsBetter
                    ? right.RawScore.CompareTo(left.RawScore)
                    : left.RawScore.CompareTo(right.RawScore);
            }

            return 0;
        }
    }
}

internal sealed record RagResultAcceptanceEvaluation(
    IReadOnlyList<RagSearchResult> Candidates,
    IReadOnlyList<RagSearchResult> AcceptedResults,
    IReadOnlyList<RagRejectedResult> RejectedResults);
