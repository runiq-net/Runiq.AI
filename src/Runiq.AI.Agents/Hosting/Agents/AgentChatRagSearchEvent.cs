using System.Text.Json.Serialization;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Core.Agents;

/// <summary>
/// Represents provider-independent RAG search lifecycle data projected to the Agent Chat transport.
/// </summary>
public sealed class AgentChatRagSearchEvent
{
    internal AgentChatRagSearchEvent(
        string agentId,
        string conversationId,
        string correlationId,
        string indexName,
        string originalQuery,
        string? effectiveQuery,
        int requestedCandidateCount)
    {
        AgentId = agentId;
        ConversationId = conversationId;
        CorrelationId = correlationId;
        IndexName = indexName;
        OriginalQuery = originalQuery;
        EffectiveQuery = effectiveQuery;
        RequestedCandidateCount = requestedCandidateCount;
    }

    /// <summary>Gets the identifier of the agent that initiated retrieval.</summary>
    public string AgentId { get; }

    /// <summary>Gets the identifier of the conversation containing retrieval.</summary>
    public string ConversationId { get; }

    /// <summary>Gets the identifier shared by events from the same retrieval lifecycle.</summary>
    public string CorrelationId { get; }

    /// <summary>Gets the effective index queried by the runtime.</summary>
    public string IndexName { get; }

    /// <summary>Gets the original query received by the runtime.</summary>
    public string OriginalQuery { get; }

    /// <summary>Gets the effective query when it differs from the original query.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EffectiveQuery { get; }

    /// <summary>Gets the maximum number of candidates requested from retrieval.</summary>
    public int RequestedCandidateCount { get; }

    /// <summary>Gets the actual number of candidates returned by a completed retrieval.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ActualCandidateCount { get; internal init; }

    /// <summary>Gets the number of candidates accepted as model context.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AcceptedCount { get; internal init; }

    /// <summary>Gets the number of candidates rejected by runtime policy.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RejectedCount { get; internal init; }

    /// <summary>Gets the configured maximum number of accepted results.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaximumAcceptedResultCount { get; internal init; }

    /// <summary>Gets the finite top raw score when one is available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopRawScore { get; internal init; }

    /// <summary>Gets the normalized top relevance when one is available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopNormalizedRelevance { get; internal init; }

    /// <summary>Gets the elapsed retrieval duration for completed or failed retrieval.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? Duration { get; internal init; }

    /// <summary>Gets the document and chunk pairs selected as model context.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AgentChatRagSelectedResult>? SelectedResults { get; internal init; }

    /// <summary>Gets the candidates rejected by runtime policy without their content.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AgentChatRagRejectedResult>? RejectedResults { get; internal init; }

    /// <summary>Gets the verifiable reason a completed retrieval produced no accepted context.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RagNoContextReason? NoContextReason { get; internal init; }

    /// <summary>Gets the provider-independent classification of a failed retrieval.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RetrievalErrorCode? FailureClassification { get; internal init; }
}

/// <summary>Identifies a document and chunk selected as model context.</summary>
public sealed class AgentChatRagSelectedResult
{
    internal AgentChatRagSelectedResult(string documentId, string chunkId)
    {
        DocumentId = documentId;
        ChunkId = chunkId;
    }

    /// <summary>Gets the selected document identifier.</summary>
    public string DocumentId { get; }

    /// <summary>Gets the selected chunk identifier.</summary>
    public string ChunkId { get; }
}

/// <summary>Describes a RAG candidate rejected by runtime policy without exposing its content.</summary>
public sealed class AgentChatRagRejectedResult
{
    internal AgentChatRagRejectedResult(
        string documentId,
        string chunkId,
        double? rawScore,
        double? normalizedRelevance,
        RagResultRejectionReason reason)
    {
        DocumentId = documentId;
        ChunkId = chunkId;
        RawScore = rawScore;
        NormalizedRelevance = normalizedRelevance;
        Reason = reason;
    }

    /// <summary>Gets the rejected document identifier.</summary>
    public string DocumentId { get; }

    /// <summary>Gets the rejected chunk identifier.</summary>
    public string ChunkId { get; }

    /// <summary>Gets the finite raw score when one is available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? RawScore { get; }

    /// <summary>Gets the normalized relevance when one is available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? NormalizedRelevance { get; }

    /// <summary>Gets the runtime policy reason for rejecting the candidate.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RagResultRejectionReason Reason { get; }
}
