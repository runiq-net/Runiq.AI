using System.Text.Json;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core.Agents;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Core.Tests.Agents;

public sealed class AgentChatRagStreamEventMapperTests
{
    [Fact]
    // Ensures a RAG search started event is projected with its complete transport identity and request data.
    public void FromExecutionEvent_ShouldMapRagSearchStarted()
    {
        var executionEvent = AgentExecutionEvent.FromRagSearch(new RagSearchStarted(
            "correlation-1", "agent-1", "conversation-1", "documents", "original", "rewritten", 20));

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Equal("rag_search_started", streamEvent.Type);
        Assert.Null(streamEvent.Content);
        Assert.Equal("agent-1", streamEvent.RagSearch!.AgentId);
        Assert.Equal("conversation-1", streamEvent.RagSearch.ConversationId);
        Assert.Equal("correlation-1", streamEvent.RagSearch.CorrelationId);
        Assert.Equal("documents", streamEvent.RagSearch.IndexName);
        Assert.Equal("original", streamEvent.RagSearch.OriginalQuery);
        Assert.Equal("rewritten", streamEvent.RagSearch.EffectiveQuery);
        Assert.Equal(20, streamEvent.RagSearch.RequestedCandidateCount);
        Assert.Null(streamEvent.ToolCallId);
    }

    [Fact]
    // Ensures a completed RAG search projects every count, score, selection, rejection, duration, and outcome field.
    public void FromExecutionEvent_ShouldMapRagSearchCompleted()
    {
        var completed = new RagSearchCompleted(
            "correlation-1", "agent-1", "conversation-1", "documents", "question", null,
            20, 2, 1, 1,
            [new RagSearchSelectedResult("document-1", "chunk-1", 0.9, 0.95, "cosine-similarity", true,
                provenance: new RagRetrievalProvenance
                {
                    Mode = RagRetrievalMode.Hybrid,
                    SemanticRank = 1,
                    LexicalRank = 2,
                    ReciprocalRankFusionScore = 0.03,
                    FusedRank = 1,
                })],
            [new RagSearchRejectedResult("document-2", "chunk-2", 0.4, 0.3, RagResultRejectionReason.BelowMinimumRelevance)],
            5, TimeSpan.FromMilliseconds(125), 0.9, 0.95, null,
            retrievalMode: RagRetrievalMode.Hybrid,
            semanticCandidateCount: 5, lexicalCandidateCount: 4, fusedCandidateCount: 7);

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(AgentExecutionEvent.FromRagSearch(completed));

        Assert.Equal("rag_search_completed", streamEvent.Type);
        var payload = streamEvent.RagSearch!;
        Assert.Equal(2, payload.ActualCandidateCount);
        Assert.Equal(1, payload.AcceptedCount);
        Assert.Equal(1, payload.RejectedCount);
        Assert.Equal(5, payload.MaximumAcceptedResultCount);
        Assert.Equal(0.9, payload.TopRawScore);
        Assert.Equal(0.95, payload.TopNormalizedRelevance);
        Assert.Equal(TimeSpan.FromMilliseconds(125), payload.Duration);
        Assert.Equal("document-1", Assert.Single(payload.SelectedResults!).DocumentId);
        var rejected = Assert.Single(payload.RejectedResults!);
        Assert.Equal("chunk-2", rejected.ChunkId);
        Assert.Equal(RagResultRejectionReason.BelowMinimumRelevance, rejected.Reason);
        Assert.Equal(0.4, rejected.RawScore);
        Assert.Equal(0.3, rejected.NormalizedRelevance);
        Assert.Null(payload.NoContextReason);
        Assert.Null(payload.FailureClassification);
        Assert.Equal(RagRetrievalMode.Hybrid, payload.RetrievalMode);
        Assert.Equal(5, payload.SemanticCandidateCount);
        Assert.Equal(4, payload.LexicalCandidateCount);
        Assert.Equal(7, payload.FusedCandidateCount);
        Assert.Equal(1, Assert.Single(payload.SelectedResults!).Provenance?.SemanticRank);
    }

    [Fact]
    // Ensures a failed RAG search remains distinct from terminal agent failure and carries only structured classification data.
    public void FromExecutionEvent_ShouldMapRagSearchFailed()
    {
        var failed = new RagSearchFailed(
            "correlation-1", "agent-1", "conversation-1", "documents", "question", null,
            20, RetrievalErrorCode.VectorStoreQueryFailed, TimeSpan.FromSeconds(2));

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(AgentExecutionEvent.FromRagSearch(failed));

        Assert.Equal("rag_search_failed", streamEvent.Type);
        Assert.Equal(RetrievalErrorCode.VectorStoreQueryFailed, streamEvent.RagSearch!.FailureClassification);
        Assert.Equal(TimeSpan.FromSeconds(2), streamEvent.RagSearch.Duration);
        Assert.Null(streamEvent.RagSearch.NoContextReason);
        Assert.Null(streamEvent.ErrorCode);
        Assert.Null(streamEvent.ErrorMessage);
    }

    [Fact]
    // Ensures RAG transport JSON uses stable discriminators and enum names without leaking content or non-finite scores.
    public void FromExecutionEvent_ShouldSerializeRagSearchWithoutSensitivePayload()
    {
        var completed = new RagSearchCompleted(
            "correlation-1", "agent-1", "conversation-1", "documents", "question", null,
            20, 1, 0, 1, [],
            [new RagSearchRejectedResult("document-1", "chunk-1", double.NaN, double.NaN, RagResultRejectionReason.InvalidScore)],
            5, TimeSpan.FromMilliseconds(10), null, null, RagNoContextReason.CandidatesRejected);
        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(AgentExecutionEvent.FromRagSearch(completed));

        var json = JsonSerializer.Serialize(streamEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"type\":\"rag_search_completed\"", json);
        Assert.Contains("\"reason\":\"InvalidScore\"", json);
        Assert.Contains("\"noContextReason\":\"CandidatesRejected\"", json);
        Assert.Contains("\"correlationId\":\"correlation-1\"", json);
        Assert.Contains("\"duration\":\"00:00:00.0100000\"", json);
        Assert.DoesNotContain("effectiveQuery", json);
        Assert.DoesNotContain("rawScore", json);
        Assert.DoesNotContain("normalizedRelevance", json);
        Assert.DoesNotContain("content preview", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stackTrace", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    // Verifies lexical-only SSE omits semantic score meanings while retaining lexical and RRF provenance.
    public void FromExecutionEvent_ShouldPreserveLexicalOnlyScoreDistinction()
    {
        var completed = new RagSearchCompleted(
            "correlation", "agent", "conversation", "documents", "query", null,
            1, 1, 1, 0,
            [new RagSearchSelectedResult("document", "chunk", provenance: new RagRetrievalProvenance
            {
                Mode = RagRetrievalMode.Hybrid,
                LexicalRank = 1,
                LexicalRawScore = 1.2,
                ReciprocalRankFusionScore = 1d / 61d,
                FusedRank = 1,
            })],
            [], 1, TimeSpan.Zero, null, null, null,
            retrievalMode: RagRetrievalMode.Hybrid,
            semanticCandidateCount: 0, lexicalCandidateCount: 1, fusedCandidateCount: 1);

        var payload = AgentChatStreamEventMapper
            .FromExecutionEvent(AgentExecutionEvent.FromRagSearch(completed)).RagSearch!;
        var selected = Assert.Single(payload.SelectedResults!);
        Assert.Null(selected.RawScore);
        Assert.Null(selected.NormalizedRelevance);
        Assert.Null(selected.Metric);
        Assert.Null(selected.HigherIsBetter);
        Assert.Equal(1.2, selected.Provenance?.LexicalRawScore);
        Assert.NotNull(selected.Provenance?.ReciprocalRankFusionScore);
    }

    [Fact]
    // Verifies unknown legacy counts are omitted from serialized SSE instead of appearing as factual zeros.
    public void FromExecutionEvent_ShouldOmitUnknownCandidateCounts()
    {
        var completed = new RagSearchCompleted(
            "correlation", "agent", "conversation", "documents", "query", null,
            1, 1, 1, 0,
            [new RagSearchSelectedResult("document", "chunk", 0.8, 0.9, "cosine-similarity", true)],
            [], 1, TimeSpan.Zero, 0.8, 0.9, null);

        var json = JsonSerializer.Serialize(
            AgentChatStreamEventMapper.FromExecutionEvent(AgentExecutionEvent.FromRagSearch(completed)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("semanticCandidateCount", json, StringComparison.Ordinal);
        Assert.DoesNotContain("lexicalCandidateCount", json, StringComparison.Ordinal);
        Assert.DoesNotContain("fusedCandidateCount", json, StringComparison.Ordinal);
    }
}
