using Runiq.ContextSpaces.Models.Skills;
using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.Agents.Runtime;

/// <summary>
/// Bir agent çalıştırması sırasında çözümlenen context space, skill ve source search bilgilerini temsil eder.
/// </summary>
public sealed record AgentRuntimeContext(
    IReadOnlyList<ContextSpace> ContextSpaces,
    IReadOnlyList<ContextSpaceSkill> Skills,
    IReadOnlyList<ContextSpaceSourceSearchResult>? SourceSearchResults = null)
{
    /// <summary>
    /// Çalıştırma sırasında kullanıcı girdisine göre bulunan source arama sonuçlarını döner.
    /// </summary>
    public IReadOnlyList<ContextSpaceSourceSearchResult> RetrievedSourceContext =>
        SourceSearchResults ?? [];

    /// <summary>
    /// Çalıştırma için herhangi bir context bilgisinin çözülüp çözülmediğini belirtir.
    /// </summary>
    public bool HasContext =>
        ContextSpaces.Count > 0 ||
        Skills.Count > 0 ||
        RetrievedSourceContext.Count > 0;
}