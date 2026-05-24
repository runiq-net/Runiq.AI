using System.Text.Json.Serialization;

namespace Runiq.Core.ContextSpaces;

/// <summary>
/// Context space source doküman envanteri yanıtını temsil eder.
/// </summary>
public sealed record ContextSpaceSourceDocumentsResponse(
    [property: JsonPropertyName("contextSpaceId")] string ContextSpaceId,
    [property: JsonPropertyName("sourceGroups")] IReadOnlyList<ContextSpaceSourceGroupDocumentDto> SourceGroups);

/// <summary>
/// Bir context source grubu ve okunabilir dokümanlarını temsil eder.
/// </summary>
public sealed record ContextSpaceSourceGroupDocumentDto(
    [property: JsonPropertyName("sourceId")] string SourceId,
    [property: JsonPropertyName("sourceName")] string SourceName,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("documentCount")] int DocumentCount,
    [property: JsonPropertyName("documents")] IReadOnlyList<ContextSpaceSourceDocumentListItemDto> Documents);

/// <summary>
/// Context source içindeki tekil doküman özet bilgisini temsil eder.
/// </summary>
public sealed record ContextSpaceSourceDocumentListItemDto(
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes,
    [property: JsonPropertyName("isPreviewSupported")] bool IsPreviewSupported);

/// <summary>
/// Context source doküman önizleme yanıtını temsil eder.
/// </summary>
public sealed record ContextSpaceSourceDocumentPreviewDto(
    [property: JsonPropertyName("contextSpaceId")] string ContextSpaceId,
    [property: JsonPropertyName("sourceId")] string SourceId,
    [property: JsonPropertyName("sourceName")] string SourceName,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("isTruncated")] bool IsTruncated,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes);
