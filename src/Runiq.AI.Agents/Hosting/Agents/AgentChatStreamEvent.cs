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
    /// Gets or initializes the structured RAG search lifecycle payload carried by RAG stream events.
    /// </summary>
    [JsonPropertyName("ragSearch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentChatRagSearchEvent? RagSearch { get; internal init; }

    /// <summary>
    /// Gets or initializes the in-process RAG policy outcome associated with terminal stream events.
    /// Agent Chat SSE uses the content-free <see cref="RagSearch"/> lifecycle projection instead.
    /// </summary>
    [JsonPropertyName("rag")]
    [JsonIgnore]
    public AgentRagExecutionMetadata? Rag { get; init; }
    /// <summary>Gets validated citations on the terminal event.</summary>
    [JsonPropertyName("citations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AgentCitation>? Citations { get; internal init; }
}
