using System.Text.Json.Serialization;

namespace Runiq.Core.Teams;

/// <summary>
/// Dashboard canlı team chat ekranına gönderilen SSE olayını temsil eder.
/// </summary>
public sealed record TeamChatStreamEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("teamId")] string TeamId,
    [property: JsonPropertyName("teamName")] string? TeamName = null,
    [property: JsonPropertyName("memberAgentId")] string? MemberAgentId = null,
    [property: JsonPropertyName("memberRole")] string? MemberRole = null,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null);