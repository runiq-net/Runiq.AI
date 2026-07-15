namespace Runiq.AI.ContextSpaces.Models.Sources;

/// <summary>
/// Context source dokümanlari içinde yapilan arama sonucunda bulunan eslesmeyi temsil eder.
/// </summary>
public sealed record ContextSpaceSourceSearchResult
{
    /// <summary>
    /// Sonucun ait oldugu source kimligini ifade eder.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// Sonucun ait oldugu source adini ifade eder.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Sonucun ait oldugu dokümanin göreli yolunu ifade eder.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Sonucun ait oldugu dosya adini ifade eder.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Aramada eslesen kisa içerik parçasini ifade eder.
    /// </summary>
    public required string Snippet { get; init; }

    /// <summary>
    /// Eslesmenin göreli skorunu ifade eder.
    /// </summary>
    public required double Score { get; init; }
}
