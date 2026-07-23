using System.Text;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Runtime;

internal static class RagContextAssembler
{
    public static RagContextAssembly Assemble(
        IReadOnlyList<RagSearchResult> acceptedResults,
        RagContextBudgetOptions options,
        int maximumContextTokens,
        int responseTokenReserve,
        int instructionsTokens,
        int conversationHistoryTokens,
        int userQueryTokens,
        int otherRequiredPromptTokens)
    {
        var mandatoryTokens = checked(instructionsTokens + conversationHistoryTokens + userQueryTokens +
            responseTokenReserve + otherRequiredPromptTokens);
        var available = maximumContextTokens - mandatoryTokens;
        if (available < 0)
        {
            var overflowBudget = new RagContextBudgetMetadata(
                maximumContextTokens, responseTokenReserve, instructionsTokens, conversationHistoryTokens,
                userQueryTokens, otherRequiredPromptTokens, 0, 0);
            return new RagContextAssembly([], acceptedResults.Select(result =>
                new RagContextExcludedResult(result, RagContextSelectionExclusionReason.TokenBudgetExceeded,
                    EstimateTokens(result.Chunk.Content))).ToArray(), overflowBudget, MandatoryPromptOverflow: true);
        }

        var selected = new List<RagSearchResult>();
        var excluded = new List<RagContextExcludedResult>();
        var sourceCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var result in OrderForSelection(acceptedResults, options.PreferSourceDiversity))
        {
            var chunkTokens = EstimateTokens(result.Chunk.Content);
            if (IsMaterialOverlap(result, selected))
            {
                excluded.Add(new(result, RagContextSelectionExclusionReason.OverlappingContent, chunkTokens));
                continue;
            }

            sourceCounts.TryGetValue(result.Chunk.DocumentId, out var sourceCount);
            if (sourceCount >= options.MaximumChunksPerSource)
            {
                excluded.Add(new(result, RagContextSelectionExclusionReason.SourceLimitExceeded, chunkTokens));
                continue;
            }

            var prospective = selected.Append(result).ToArray();
            var assembled = AgentInstructionsBuilder.BuildExternalContext(prospective);
            var assembledTokens = EstimateTokens(assembled);
            if (assembledTokens > available)
            {
                excluded.Add(new(result, RagContextSelectionExclusionReason.TokenBudgetExceeded, chunkTokens));
                continue;
            }

            selected.Add(result);
            sourceCounts[result.Chunk.DocumentId] = sourceCount + 1;
        }

        var finalContext = AgentInstructionsBuilder.BuildExternalContext(selected);
        var selectedTokens = EstimateTokens(finalContext);
        var metadata = new RagContextBudgetMetadata(
            maximumContextTokens, responseTokenReserve, instructionsTokens, conversationHistoryTokens,
            userQueryTokens, otherRequiredPromptTokens, available, selectedTokens);
        return new RagContextAssembly(selected.ToArray(), excluded.ToArray(), metadata, MandatoryPromptOverflow: false);
    }

    // This deterministic fallback counts contiguous Unicode letter/digit runs and individual punctuation marks.
    // It intentionally makes no claim of provider-tokenizer exactness and never invokes a model.
    internal static int EstimateTokens(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        var count = 0;
        var inWord = false;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (!inWord) count++;
                inWord = true;
            }
            else
            {
                inWord = false;
                if (!char.IsWhiteSpace(character)) count++;
            }
        }
        return count;
    }

    private static IEnumerable<RagSearchResult> OrderForSelection(
        IReadOnlyList<RagSearchResult> results,
        bool preferDiversity)
    {
        if (!preferDiversity) return results;
        var groups = new List<List<RagSearchResult>>();
        var bySource = new Dictionary<string, List<RagSearchResult>>(StringComparer.Ordinal);
        foreach (var result in results)
        {
            if (!bySource.TryGetValue(result.Chunk.DocumentId, out var group))
            {
                group = [];
                bySource.Add(result.Chunk.DocumentId, group);
                groups.Add(group);
            }
            group.Add(result);
        }

        var ordered = new List<RagSearchResult>(results.Count);
        for (var round = 0; ordered.Count < results.Count; round++)
            foreach (var group in groups)
                if (round < group.Count) ordered.Add(group[round]);
        return ordered;
    }

    private static bool IsMaterialOverlap(RagSearchResult candidate, IReadOnlyList<RagSearchResult> selected)
    {
        var start = candidate.Chunk.Metadata.StartIndex;
        var end = candidate.Chunk.Metadata.EndIndex;
        if (start is null || end is null || end <= start) return false;

        foreach (var existing in selected)
        {
            if (!StringComparer.Ordinal.Equals(candidate.Chunk.DocumentId, existing.Chunk.DocumentId)) continue;
            var existingStart = existing.Chunk.Metadata.StartIndex;
            var existingEnd = existing.Chunk.Metadata.EndIndex;
            if (existingStart is null || existingEnd is null || existingEnd <= existingStart) continue;
            var intersection = Math.Max(0, Math.Min(end.Value, existingEnd.Value) - Math.Max(start.Value, existingStart.Value));
            var shorter = Math.Min(end.Value - start.Value, existingEnd.Value - existingStart.Value);
            if (intersection * 2 >= shorter) return true;
        }
        return false;
    }
}

internal sealed record RagContextAssembly(
    IReadOnlyList<RagSearchResult> SelectedResults,
    IReadOnlyList<RagContextExcludedResult> ExcludedResults,
    RagContextBudgetMetadata? Budget,
    bool MandatoryPromptOverflow);
