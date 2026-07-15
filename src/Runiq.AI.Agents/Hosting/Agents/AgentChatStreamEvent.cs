using System.Text.Json.Serialization;

namespace Runiq.AI.Core.Agents;

/// <summary>
/// Dashboard canli chat ekranina gönderilen SSE olayini temsil eder.
/// </summary>
public sealed record AgentChatStreamEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("toolCallId")] string? ToolCallId = null,
    [property: JsonPropertyName("toolName")] string? ToolName = null,
    [property: JsonPropertyName("argumentsJson")] string? ArgumentsJson = null,
    [property: JsonPropertyName("outputJson")] string? OutputJson = null,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null,
    [property: JsonPropertyName("contextSpaces")] IReadOnlyList<AgentChatContextSpaceStreamItem>? ContextSpaces = null,
    [property: JsonPropertyName("skills")] IReadOnlyList<AgentChatSkillStreamItem>? Skills = null,
    [property: JsonPropertyName("sources")] IReadOnlyList<AgentChatSourceStreamItem>? Sources = null,
    [property: JsonPropertyName("loadedSkills")] IReadOnlyList<AgentChatLoadedSkillStreamItem>? LoadedSkills = null,
    [property: JsonPropertyName("contextSearchSummary")] AgentChatContextSearchSummaryStreamItem? ContextSearchSummary = null,
    [property: JsonPropertyName("sourceSearchResults")] IReadOnlyList<AgentChatSourceSearchResultStreamItem>? SourceSearchResults = null);


/// <summary>
/// Chat SSE olayinda gösterilecek context space özet bilgisini temsil eder.
/// </summary>
public sealed record AgentChatContextSpaceStreamItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description);

/// <summary>
/// Chat SSE olayinda gösterilecek skill özet bilgisini temsil eder.
/// </summary>
public sealed record AgentChatSkillStreamItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("sourceId")] string SourceId,
    [property: JsonPropertyName("relativePath")] string RelativePath);

/// <summary>
/// Chat SSE olayinda gösterilecek yüklenen skill özet bilgisini temsil eder.
/// </summary>
public sealed record AgentChatLoadedSkillStreamItem(
    [property: JsonPropertyName("skillId")] string SkillId,
    [property: JsonPropertyName("skillName")] string SkillName,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("description")] string? Description);

/// <summary>
/// Chat SSE olayinda gösterilecek source özet bilgisini temsil eder.
/// </summary>
public sealed record AgentChatSourceStreamItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("description")] string? Description);

/// <summary>
/// Chat SSE olayinda gösterilecek context source arama özetini temsil eder.
/// </summary>
public sealed record AgentChatContextSearchSummaryStreamItem(
    [property: JsonPropertyName("attachedSourceCount")] int AttachedSourceCount,
    [property: JsonPropertyName("searchedDocumentCount")] int SearchedDocumentCount,
    [property: JsonPropertyName("candidateCount")] int CandidateCount,
    [property: JsonPropertyName("selectedCount")] int SelectedCount);

/// <summary>
/// Chat SSE olayinda gösterilecek source arama sonucunu temsil eder.
/// </summary>
public sealed record AgentChatSourceSearchResultStreamItem(
    [property: JsonPropertyName("sourceId")] string SourceId,
    [property: JsonPropertyName("sourceName")] string SourceName,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("snippet")] string Snippet,
    [property: JsonPropertyName("score")] double Score);

