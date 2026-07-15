using Runiq.AI.ContextSpaces.Models.Skills;
using Runiq.AI.ContextSpaces.Models.Sources;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Runtime;

/// <summary>
/// Bir agent çalıştırması sırasında çözümlenen context space, skill ve source search bilgilerini temsil eder.
/// </summary>
public sealed record AgentRuntimeContext(
    IReadOnlyList<ContextSpace> ContextSpaces,
    IReadOnlyList<ContextSpaceSkill> Skills,
    int AttachedSourceCount = 0,
    int SearchedDocumentCount = 0,
    int CandidateCount = 0,
    IReadOnlyList<ContextSpaceSourceSearchResult>? SourceSearchResults = null,
    IReadOnlyList<RagSearchResult>? RagSearchResults = null)
{
    /// <summary>
    /// Çalıştırma sırasında kullanıcı girdisine göre bulunan source arama sonuçlarını döner.
    /// </summary>
    public IReadOnlyList<ContextSpaceSourceSearchResult> RetrievedSourceContext =>
        SourceSearchResults ?? [];

    /// <summary>
    /// Çalıştırma sırasında kullanıcı girdisine göre bulunan RAG arama sonuçlarını döner.
    /// </summary>
    public IReadOnlyList<RagSearchResult> RetrievedRagContext =>
        RagSearchResults ?? [];

    /// <summary>
    /// Çalıştırma için herhangi bir context bilgisinin çözülüp çözülmediğini belirtir.
    /// </summary>
    public bool HasContext =>
        ContextSpaces.Count > 0 ||
        Skills.Count > 0 ||
        RetrievedSourceContext.Count > 0 ||
        RetrievedRagContext.Count > 0;

    /// <summary>
    /// Model context'ine eklenmek üzere seçilen source excerpt sayısını döner.
    /// </summary>
    public int SelectedCount => RetrievedSourceContext.Count;
}

