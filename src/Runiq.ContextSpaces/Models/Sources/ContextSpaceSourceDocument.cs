namespace Runiq.ContextSpaces.Models.Sources;

/// <summary>
/// Context space içindeki bir source üzerinden okunmuş doküman içeriğini temsil eder.
/// </summary>
public sealed record ContextSpaceSourceDocument
{
    /// <summary>
    /// Dokümanın ait olduğu context source kimliğini ifade eder.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// Dokümanın ait olduğu context source adını ifade eder.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Dokümanın source kök klasörüne göre göreli yolunu ifade eder.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Dokümanın dosya adını ifade eder.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Dokümanın dosya uzantısını ifade eder.
    /// </summary>
    public required string Extension { get; init; }

    /// <summary>
    /// Dokümanın içerik tipini ifade eder.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Dokümanın normalize edilmiş metin içeriğini ifade eder.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Dokümanın byte cinsinden dosya boyutunu ifade eder.
    /// </summary>
    public required long SizeInBytes { get; init; }
}