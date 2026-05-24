using System.Text.Json.Serialization;

namespace Runiq.Core.Agents;

/// <summary>
/// Dashboard canlı chat ekranına gönderilen SSE olayını temsil eder.
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
    [property: JsonPropertyName("sourceSearchResults")] IReadOnlyList<AgentChatSourceSearchResultStreamItem>? SourceSearchResults = null);


/// <summary>
/// Chat SSE olayında gösterilecek context space özet bilgisini temsil eder.
/// </summary>
public sealed record AgentChatContextSpaceStreamItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description);

/// <summary>
/// Chat SSE olayında gösterilecek skill özet bilgisini temsil eder.
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
/// Chat SSE olayında gösterilecek source özet bilgisini temsil eder.
/// </summary>
public sealed record AgentChatSourceStreamItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("description")] string? Description);


/// <summary>
/// Chat SSE olayında gösterilecek source arama sonucunu temsil eder.
/// </summary>
public sealed record AgentChatSourceSearchResultStreamItem(
    [property: JsonPropertyName("sourceId")] string SourceId,
    [property: JsonPropertyName("sourceName")] string SourceName,
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("snippet")] string Snippet,
    [property: JsonPropertyName("score")] double Score);