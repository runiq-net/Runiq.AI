using System.Text.Json.Serialization;
using Runiq.AI.Agents;

namespace Runiq.AI.Core.Agents;

/// <summary>
/// Dashboard canli chat ekranina gonderilen SSE olayini temsil eder.
/// </summary>
public sealed record AgentChatStreamEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("toolCallId")] string? ToolCallId = null,
    [property: JsonPropertyName("toolName")] string? ToolName = null,
    [property: JsonPropertyName("argumentsJson")] string? ArgumentsJson = null,
    [property: JsonPropertyName("outputJson")] string? OutputJson = null,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null)
{
    /// <summary>
    /// Gets or initializes the structured RAG policy outcome carried by terminal stream events.
    /// </summary>
    [JsonPropertyName("rag")]
    public AgentRagExecutionMetadata? Rag { get; init; }
}
