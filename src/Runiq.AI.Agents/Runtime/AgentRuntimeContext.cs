using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Runtime;

/// <summary>
/// Bir agent calistirmasi sirasinda cozumlenen RAG context bilgilerini temsil eder.
/// </summary>
public sealed record AgentRuntimeContext(
    IReadOnlyList<RagSearchResult>? RagSearchResults = null)
{
    /// <summary>
    /// Calistirma sirasinda kullanici girdisine gore bulunan RAG arama sonuclarini doner.
    /// </summary>
    public IReadOnlyList<RagSearchResult> RetrievedRagContext =>
        RagSearchResults ?? [];

    /// <summary>
    /// Calistirma icin herhangi bir context bilgisinin cozulup cozulmedigini belirtir.
    /// </summary>
    public bool HasContext => RetrievedRagContext.Count > 0;
}