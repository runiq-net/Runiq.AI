using System.Text.Json.Serialization;

namespace Runiq.Core.ContextSpaces;

/// <summary>
/// Context space skill dokÃ¼man envanteri yanÄ±tÄ±nÄ± temsil eder.
/// </summary>
public sealed record ContextSpaceSkillDocumentsResponse(
    [property: JsonPropertyName("contextSpaceId")] string ContextSpaceId,
    [property: JsonPropertyName("skillSources")] IReadOnlyList<ContextSpaceSkillSourceDocumentDto> SkillSources);

/// <summary>
/// Bir skill source grubu ve keÅŸfedilen skill dokÃ¼manlarÄ±nÄ± temsil eder.
/// </summary>
public sealed record ContextSpaceSkillSourceDocumentDto(
    [property: JsonPropertyName("sourceId")] string SourceId,
    [property: JsonPropertyName("sourceName")] string SourceName,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("skillCount")] int SkillCount,
    [property: JsonPropertyName("skills")] IReadOnlyList<ContextSpaceSkillDocumentListItemDto> Skills);

/// <summary>
/// KeÅŸfedilmiÅŸ skill dokÃ¼man Ã¶zetini temsil eder.
/// </summary>
public sealed record ContextSpaceSkillDocumentListItemDto(
    [property: JsonPropertyName("skillId")] string SkillId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("isPreviewSupported")] bool IsPreviewSupported);

/// <summary>
/// Skill dokÃ¼man Ã¶nizleme yanÄ±tÄ±nÄ± temsil eder.
/// </summary>
public sealed record ContextSpaceSkillDocumentPreviewDto(
    [property: JsonPropertyName("contextSpaceId")] string ContextSpaceId,
    [property: JsonPropertyName("skillId")] string SkillId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("isTruncated")] bool IsTruncated,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes);
