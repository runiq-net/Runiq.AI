using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Runtime;

/// <summary>
/// Bir agent calistirmasi sirasinda cozumlenen RAG context bilgilerini temsil eder.
/// </summary>
public sealed record AgentRuntimeContext(
    IReadOnlyList<RagSearchResult>? RagSearchResults = null)
{
    internal AgentRuntimeContext(
        IReadOnlyList<RagSearchResult> acceptedResults,
        IReadOnlyList<RagSearchResult> candidates,
        IReadOnlyList<RagRejectedResult> rejectedResults,
        Configuration.RagNoContextReason? noContextReason)
        : this(acceptedResults)
    {
        RetrievedRagCandidates = candidates;
        RejectedRagCandidates = rejectedResults;
        NoContextReason = noContextReason;
    }

    /// <summary>
    /// Calistirma sirasinda kullanici girdisine gore bulunan RAG arama sonuclarini doner.
    /// </summary>
    public IReadOnlyList<RagSearchResult> RetrievedRagContext =>
        RagSearchResults ?? [];

    /// <summary>
    /// Gets the raw retrieval candidates before the runtime applies context acceptance criteria.
    /// </summary>
    internal IReadOnlyList<RagSearchResult> RetrievedRagCandidates { get; } =
        RagSearchResults ?? [];

    internal IReadOnlyList<RagRejectedResult> RejectedRagCandidates { get; } = [];

    internal Configuration.RagNoContextReason? NoContextReason { get; }

    /// <summary>
    /// Calistirma icin herhangi bir context bilgisinin cozulup cozulmedigini belirtir.
    /// </summary>
    public bool HasContext => RetrievedRagContext.Count > 0;
}
