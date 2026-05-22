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
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null);