using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tests.TestDoubles;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class RagSearchCompletedProjectionTests
{
    [Fact]
    // Verifies selected results preserve accepted context order while duplicate and limited candidates remain excluded from the model request.
    public async Task ExecuteStreamAsync_ShouldProjectAcceptedOrderAndRejectionDecisions()
    {
        var candidates = new[]
        {
            CreateCandidate("chunk-limit", "document-d", "limit excluded", 0.70),
            CreateCandidate("chunk-duplicate", "document-c", "accepted first", 0.85),
            CreateCandidate("chunk-second", "document-b", "accepted second", 0.80),
            CreateCandidate("chunk-first", "document-a", "accepted first", 0.90),
        };
        var agent = CreateAgent(maximumAcceptedResults: 2);
        var client = new ScriptedChatClient();

        var events = await CreateRuntime(agent, client, candidates)
            .ExecuteStreamAsync(agent.Id, "question")
            .ToListAsync();

        var completed = Assert.IsType<RagSearchCompleted>(events[1].RagSearch);
        Assert.Equal(4, completed.ActualCandidateCount);
        Assert.Equal(2, completed.AcceptedCount);
        Assert.Equal(2, completed.RejectedCount);
        Assert.Equal(completed.ActualCandidateCount, completed.AcceptedCount + completed.RejectedCount);
        Assert.Equal(
            [("document-a", "chunk-first"), ("document-b", "chunk-second")],
            completed.SelectedResults.Select(result => (result.DocumentId, result.ChunkId)));
        Assert.Contains(completed.RejectedResults, result =>
            result.ChunkId == "chunk-duplicate" && result.Reason == RagResultRejectionReason.DuplicateContent);
        Assert.Contains(completed.RejectedResults, result =>
            result.ChunkId == "chunk-limit" && result.Reason == RagResultRejectionReason.ResultLimitExceeded);
        Assert.Empty(completed.SelectedResults.Select(result => result.ChunkId)
            .Intersect(completed.RejectedResults.Select(result => result.ChunkId)));
        Assert.Equal(0.90, completed.TopRawScore);
        Assert.Equal(0.95, completed.TopNormalizedRelevance);

        var context = Assert.Single(client.Requests).Messages.Single(message =>
            message.Content.Contains("<untrusted-external-context>", StringComparison.Ordinal));
        Assert.True(context.Content.IndexOf("chunk-first", StringComparison.Ordinal) <
            context.Content.IndexOf("chunk-second", StringComparison.Ordinal));
        Assert.DoesNotContain("chunk-duplicate", context.Content);
        Assert.DoesNotContain("chunk-limit", context.Content);
    }

    [Fact]
    // Verifies invalid and below-threshold candidates remain visible with truthful reasons and top scores come from the highest-ranked valid candidate.
    public async Task ExecuteStreamAsync_ShouldProjectInvalidAndThresholdRejectionsWithValidTopScore()
    {
        var candidates = new[]
        {
            CreateCandidate("chunk-invalid", "document-a", "invalid", double.NaN),
            CreateCandidate("chunk-threshold", "document-b", "below threshold", 0.0),
        };
        var agent = CreateAgent(minimumRelevance: 0.90);

        var events = await CreateRuntime(agent, new ScriptedChatClient(), candidates)
            .ExecuteStreamAsync(agent.Id, "question")
            .ToListAsync();

        var completed = Assert.IsType<RagSearchCompleted>(events[1].RagSearch);
        Assert.Equal(2, completed.ActualCandidateCount);
        Assert.Equal(0, completed.AcceptedCount);
        Assert.Equal(2, completed.RejectedCount);
        Assert.Empty(completed.SelectedResults);
        Assert.Contains(completed.RejectedResults, result =>
            result.ChunkId == "chunk-invalid" && result.Reason == RagResultRejectionReason.InvalidScore);
        Assert.Contains(completed.RejectedResults, result =>
            result.ChunkId == "chunk-threshold" && result.Reason == RagResultRejectionReason.BelowMinimumRelevance);
        Assert.Equal(RagNoContextReason.CandidatesRejected, completed.NoContextReason);
        Assert.Equal(0.0, completed.TopRawScore);
        Assert.Equal(0.5, completed.TopNormalizedRelevance);
    }

    [Fact]
    // Verifies the first acceptable chunk identity is selected and only its later occurrence is rejected as duplicate.
    public async Task ExecuteStreamAsync_ShouldAcceptFirstChunkIdentityAndRejectLaterOccurrence()
    {
        var candidates = new[]
        {
            CreateCandidate("chunk-shared", "document-a", "first version", 0.90),
            CreateCandidate("chunk-shared", "document-a", "second version", 0.80),
        };
        var agent = CreateAgent();
        var client = new ScriptedChatClient();

        var events = await CreateRuntime(agent, client, candidates)
            .ExecuteStreamAsync(agent.Id, "question")
            .ToListAsync();

        var completed = Assert.IsType<RagSearchCompleted>(events[1].RagSearch);
        var selected = Assert.Single(completed.SelectedResults);
        Assert.Equal(("document-a", "chunk-shared"), (selected.DocumentId, selected.ChunkId));
        var rejected = Assert.Single(completed.RejectedResults);
        Assert.Equal(RagResultRejectionReason.DuplicateContent, rejected.Reason);
        Assert.Equal(0.80, rejected.RawScore);
        Assert.Null(completed.NoContextReason);

        var context = Assert.Single(client.Requests).Messages.Single(message =>
            message.Content.Contains("<untrusted-external-context>", StringComparison.Ordinal));
        Assert.Contains("first version", context.Content);
        Assert.DoesNotContain("second version", context.Content);
    }

    [Fact]
    // Verifies streaming completed projection and non-streaming metadata describe the same accepted and rejected runtime outcome.
    public async Task ExecutePaths_ShouldExposeEquivalentCompletedSelection()
    {
        var candidates = new[]
        {
            CreateCandidate("chunk-first", "document-a", "accepted", 0.90),
            CreateCandidate("chunk-rejected", "document-b", "rejected", 0.0),
        };
        var agent = CreateAgent(minimumRelevance: 0.75);
        var result = await CreateRuntime(agent, new ScriptedChatClient(), candidates)
            .ExecuteAsync(agent, "question");
        var events = await CreateRuntime(agent, new ScriptedChatClient(), candidates)
            .ExecuteStreamAsync(agent.Id, "question")
            .ToListAsync();

        var completed = Assert.IsType<RagSearchCompleted>(events[1].RagSearch);
        Assert.Equal(result.Rag!.CandidateCount, completed.ActualCandidateCount);
        Assert.Equal(result.Rag.AcceptedCount, completed.AcceptedCount);
        Assert.Equal(result.Rag.RejectedCount, completed.RejectedCount);
        Assert.Equal(
            result.Rag.AcceptedResults.Select(item => (item.Chunk.DocumentId, item.Chunk.Id)),
            completed.SelectedResults.Select(item => (item.DocumentId, item.ChunkId)));
        Assert.Equal(
            result.Rag.RejectedResults.Select(item => (item.Result.Chunk.DocumentId, item.Result.Chunk.Id, item.Reason)),
            completed.RejectedResults.Select(item => (item.DocumentId, item.ChunkId, item.Reason)));
    }

    [Fact]
    // Verifies an empty retrieval remains distinct from an all-rejected retrieval in the completed payload.
    public async Task ExecuteStreamAsync_ShouldDistinguishEmptyFromAllRejectedOutcome()
    {
        var emptyAgent = CreateAgent();
        var emptyEvents = await CreateRuntime(emptyAgent, new ScriptedChatClient(), [])
            .ExecuteStreamAsync(emptyAgent.Id, "question")
            .ToListAsync();
        var empty = Assert.IsType<RagSearchCompleted>(emptyEvents[1].RagSearch);

        var rejectedAgent = CreateAgent(minimumRelevance: 0.99);
        var rejectedEvents = await CreateRuntime(
                rejectedAgent,
                new ScriptedChatClient(),
                [CreateCandidate("chunk-low", "document-a", "low", 0.0)])
            .ExecuteStreamAsync(rejectedAgent.Id, "question")
            .ToListAsync();
        var rejected = Assert.IsType<RagSearchCompleted>(rejectedEvents[1].RagSearch);

        Assert.Equal(RagNoContextReason.NoResults, empty.NoContextReason);
        Assert.Equal(0, empty.ActualCandidateCount);
        Assert.Equal(RagNoContextReason.BelowRelevanceThreshold, rejected.NoContextReason);
        Assert.Equal(1, rejected.ActualCandidateCount);
        Assert.Equal(1, rejected.RejectedCount);
    }

    private static Agent CreateAgent(
        double? minimumRelevance = null,
        int maximumAcceptedResults = 5) =>
        new Agent("agent", "Agent", "trusted instructions", "openai/model", "key")
            .UseRag(options =>
            {
                options.IndexName = "documents";
                options.Acceptance.MinimumRelevance = minimumRelevance;
                options.Acceptance.MaximumAcceptedResults = maximumAcceptedResults;
            });

    private static AgentExecutionRuntime CreateRuntime(
        Agent agent,
        IChatClient client,
        IReadOnlyList<RagSearchResult> candidates) =>
        new(
            [agent],
            new TestChatClientResolver(client),
            new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()),
            new StaticRetriever(candidates));

    private static RagSearchResult CreateCandidate(
        string chunkId,
        string documentId,
        string content,
        double rawScore) =>
        new()
        {
            Chunk = new RagChunk { Id = chunkId, DocumentId = documentId, Content = content },
            RawScore = rawScore,
            Metric = RagScoreMetrics.CosineSimilarity,
            HigherIsBetter = true,
        };

    private sealed class StaticRetriever(IReadOnlyList<RagSearchResult> candidates) : IRagRetriever
    {
        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(candidates);
    }
}
