using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents;

/// <summary>
/// Associates an accepted retrieval result with its context-selection exclusion reason.
/// </summary>
public sealed class RagContextExcludedResult
{
    /// <summary>Initializes an accepted but context-excluded result.</summary>
    /// <param name="result">The accepted retrieval result.</param>
    /// <param name="reason">The deterministic context-selection exclusion reason.</param>
    /// <param name="estimatedTokens">The estimated token count of the complete chunk content.</param>
    public RagContextExcludedResult(
        RagSearchResult result,
        RagContextSelectionExclusionReason reason,
        int estimatedTokens)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        if (!Enum.IsDefined(reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        if (estimatedTokens < 0) throw new ArgumentOutOfRangeException(nameof(estimatedTokens));
        Reason = reason;
        EstimatedTokens = estimatedTokens;
    }

    /// <summary>Gets the accepted retrieval result.</summary>
    public RagSearchResult Result { get; }

    /// <summary>Gets the context-selection exclusion reason.</summary>
    public RagContextSelectionExclusionReason Reason { get; }

    /// <summary>Gets the deterministic estimated token count of the complete chunk content.</summary>
    public int EstimatedTokens { get; }
}
