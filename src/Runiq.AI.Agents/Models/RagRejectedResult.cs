using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents;

/// <summary>
/// Associates a raw RAG candidate with the explicit reason it was not accepted as Agent Chat context.
/// </summary>
public sealed class RagRejectedResult
{
    /// <summary>
    /// Initializes a rejected candidate result.
    /// </summary>
    /// <param name="result">The rejected retrieval candidate.</param>
    /// <param name="reason">The reason the candidate was rejected.</param>
    public RagRejectedResult(RagSearchResult result, RagResultRejectionReason reason)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        Reason = reason;
    }

    /// <summary>
    /// Gets the rejected retrieval candidate, including raw score and normalized relevance information.
    /// </summary>
    public RagSearchResult Result { get; }

    /// <summary>
    /// Gets the reason the candidate was rejected.
    /// </summary>
    public RagResultRejectionReason Reason { get; }
}
