using System.Text.Json.Serialization;
using System.Text.Json;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents;

namespace Runiq.AI.Core.Agents;

/// <summary>
/// Represents an Agent Chat SSE event, preserving legacy fields alongside optional run metadata.
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
    /// <summary>Gets the runtime invocation identifier, or null for standalone legacy events.</summary>
    [JsonPropertyName("runId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public string? RunId { get; internal init; }

    /// <summary>Gets the reusable agent definition identifier.</summary>
    [JsonPropertyName("agentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public string? AgentId { get; internal init; }

    /// <summary>Gets the run state represented by the event, independently of tool step status.</summary>
    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(JsonStringEnumConverter<AgentRunStatus>))]
    [JsonInclude]
    public AgentRunStatus? Status { get; internal init; }

    /// <summary>Gets the one-based event sequence within the run.</summary>
    [JsonPropertyName("sequenceNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public long? SequenceNumber { get; internal init; }

    /// <summary>Gets the UTC time when runtime published this event.</summary>
    [JsonPropertyName("timestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public DateTimeOffset? Timestamp { get; internal init; }

    /// <summary>Gets the UTC run start time, or null for a standalone factory event.</summary>
    [JsonPropertyName("startedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public DateTimeOffset? StartedAt { get; internal init; }

    /// <summary>Gets the UTC terminal transition time; omitted on running events.</summary>
    [JsonPropertyName("endedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public DateTimeOffset? EndedAt { get; internal init; }

    /// <summary>Gets the complete text on a successful terminal event; content retains its legacy meaning.</summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public string? Message { get; internal init; }

    /// <summary>Gets explicit completion JSON backed by the execution event's independently owned data.</summary>
    [JsonPropertyName("structuredOutput")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonInclude]
    public JsonElement? StructuredOutput { get; internal init; }

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
