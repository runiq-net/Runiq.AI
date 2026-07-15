namespace Runiq.AI.ContextSpaces.Models.Sources;

/// <summary>
/// Context space içindeki bir source üzerinden okunmus doküman içerigini temsil eder.
/// </summary>
public sealed record ContextSpaceSourceDocument
{
    /// <summary>
    /// Dokümanin ait oldugu context source kimligini ifade eder.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// Dokümanin ait oldugu context source adini ifade eder.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Dokümanin source kök klasörüne göre göreli yolunu ifade eder.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Dokümanin dosya adini ifade eder.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Dokümanin dosya uzantisini ifade eder.
    /// </summary>
    public required string Extension { get; init; }

    /// <summary>
    /// Dokümanin içerik tipini ifade eder.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Dokümanin normalize edilmis metin içerigini ifade eder.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Dokümanin byte cinsinden dosya boyutunu ifade eder.
    /// </summary>
    public required long SizeInBytes { get; init; }
}
