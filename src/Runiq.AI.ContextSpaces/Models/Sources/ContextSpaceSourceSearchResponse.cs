namespace Runiq.AI.ContextSpaces.Models.Sources;

/// <summary>
/// Context source dokümanları üzerinde yapılan aramanın sonuç ve özet bilgisini temsil eder.
/// </summary>
public sealed record ContextSpaceSourceSearchResponse(
    int SearchedDocumentCount,
    IReadOnlyList<ContextSpaceSourceSearchResult> Results);

