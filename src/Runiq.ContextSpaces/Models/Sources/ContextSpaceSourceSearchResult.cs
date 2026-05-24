namespace Runiq.ContextSpaces.Models.Sources;

/// <summary>
/// Context source dokümanları içinde yapılan arama sonucunda bulunan eşleşmeyi temsil eder.
/// </summary>
public sealed record ContextSpaceSourceSearchResult
{
    /// <summary>
    /// Sonucun ait olduğu source kimliğini ifade eder.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// Sonucun ait olduğu source adını ifade eder.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Sonucun ait olduğu dokümanın göreli yolunu ifade eder.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Sonucun ait olduğu dosya adını ifade eder.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Aramada eşleşen kısa içerik parçasını ifade eder.
    /// </summary>
    public required string Snippet { get; init; }

    /// <summary>
    /// Eşleşmenin göreli skorunu ifade eder.
    /// </summary>
    public required double Score { get; init; }
}