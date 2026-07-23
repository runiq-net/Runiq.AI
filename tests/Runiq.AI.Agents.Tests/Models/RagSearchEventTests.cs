using Runiq.AI.Agents.Configuration;
using Runiq.AI.Rag.Models.Retrieval;
using System.Reflection;
using Runiq.AI.Rag.Runtime;

namespace Runiq.AI.Agents.Tests.Models;

public sealed class RagSearchEventTests
{
    // Ensures blocked readiness cannot represent usable states or a contradictory suggested action.
    [Fact]
    public void RagSearchBlocked_ShouldRejectUsableOrContradictoryStates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateBlocked(RagIndexReadiness.Ready, RagReadinessSuggestedAction.StartIngestion));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateBlocked(RagIndexReadiness.Degraded, RagReadinessSuggestedAction.StartIngestion));
        Assert.Throws<ArgumentException>(() => CreateBlocked(RagIndexReadiness.NotInitialized, RagReadinessSuggestedAction.RetryIngestion));
        Assert.Throws<ArgumentException>(() => CreateBlocked(null, RagReadinessSuggestedAction.StartIngestion));
    }

    // Ensures readiness progress rejects negative and internally inconsistent count snapshots.
    [Fact]
    public void RagReadinessProgress_ShouldRejectInvalidCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RagReadinessProgress(-1, 0, 0));
        Assert.Throws<ArgumentException>(() => new RagReadinessProgress(1, 2, 0));
        Assert.Throws<ArgumentException>(() => new RagReadinessProgress(2, 1, 2));
    }

    // Ensures active operation and safe failure fields remain confined to their meaningful readiness states.
    [Fact]
    public void RagSearchBlocked_ShouldRejectContradictoryOptionalData()
    {
        Assert.Throws<ArgumentException>(() => new RagSearchBlocked("correlation", "agent", "conversation", "index", null, null, 1,
            RagIndexReadiness.NotInitialized, "NotInitialized", RagReadinessSuggestedAction.StartIngestion,
            activeOperationState: RagIngestionOperationState.Running));
        Assert.Throws<ArgumentException>(() => new RagSearchBlocked("correlation", "agent", "conversation", "index", null, null, 1,
            RagIndexReadiness.Initializing, "Initializing", RagReadinessSuggestedAction.WaitForIngestion,
            safeFailureSummary: "failure"));
    }
    [Fact]
    // Ensures RAG lifecycle payloads attach to the existing execution event stream through a type-safe composition point.
    public void ExecutionEvent_ShouldCarryTypedRagSearchPayload()
    {
        var payload = CreateStarted();

        var executionEvent = AgentExecutionEvent.FromRagSearch(payload);

        Assert.Equal(AgentExecutionEventKind.RagSearch, executionEvent.Kind);
        Assert.Same(payload, executionEvent.RagSearch);
        Assert.Null(executionEvent.ToolCallId);
        Assert.Null(executionEvent.ToolName);
    }

    [Fact]
    // Ensures a regular execution event never carries a RAG search lifecycle payload.
    public void ExecutionEvent_ShouldKeepRagSearchNull_ForNonRagFactory()
    {
        var executionEvent = AgentExecutionEvent.AssistantDelta("hello");

        Assert.Equal(AgentExecutionEventKind.AssistantDelta, executionEvent.Kind);
        Assert.Null(executionEvent.RagSearch);
    }

    [Fact]
    // Ensures the RAG search factory rejects a missing lifecycle payload.
    public void ExecutionEvent_ShouldRejectNullRagSearchPayload()
    {
        Assert.Throws<ArgumentNullException>(() => AgentExecutionEvent.FromRagSearch(null!));
    }

    [Fact]
    // Ensures callers cannot use a public constructor to create a contradictory kind and payload state.
    public void ExecutionEvent_ShouldExposeNoPublicConstructors()
    {
        Assert.Empty(typeof(AgentExecutionEvent).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    // Ensures the controlled constructor rejects a RAG search kind without its required payload.
    public void ExecutionEvent_ShouldRejectRagSearchKindWithoutPayload()
    {
        var constructor = Assert.Single(
            typeof(AgentExecutionEvent).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance),
            candidate => candidate.GetParameters().Length == 10);
        var arguments = new object?[]
        {
            AgentExecutionEventKind.RagSearch, null, null, null, null, null, null, null, null, null,
        };

        var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(arguments));
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    // Ensures callers cannot attach or replace a RAG search payload through an initializer or record with-expression.
    public void ExecutionEvent_ShouldExposeReadOnlyRagSearchProperty()
    {
        var property = typeof(AgentExecutionEvent).GetProperty(nameof(AgentExecutionEvent.RagSearch));

        Assert.NotNull(property);
        Assert.Null(property.SetMethod);
    }

    [Fact]
    // Ensures existing execution event factories retain their kinds and never carry RAG search payloads.
    public void ExecutionEvent_ShouldPreserveExistingFactoryContracts()
    {
        var events = new[]
        {
            AgentExecutionEvent.AssistantDelta("hello"),
            AgentExecutionEvent.ToolCallStarted("call-1", "tool", "{}"),
            AgentExecutionEvent.ToolCallCompleted("call-1", "tool", "{}"),
            AgentExecutionEvent.ToolCallFailed("call-1", "tool", "failed"),
            AgentExecutionEvent.Completed(),
            AgentExecutionEvent.Failed("failed"),
        };

        Assert.Equal(
            [AgentExecutionEventKind.AssistantDelta, AgentExecutionEventKind.ToolCallStarted,
                AgentExecutionEventKind.ToolCallCompleted, AgentExecutionEventKind.ToolCallFailed,
                AgentExecutionEventKind.Completed, AgentExecutionEventKind.Failed],
            events.Select(executionEvent => executionEvent.Kind));
        Assert.All(events, executionEvent => Assert.Null(executionEvent.RagSearch));
    }

    [Fact]
    // Ensures lifecycle payloads can be correlated while separate retrievals remain distinguishable in one conversation.
    public void LifecyclePayloads_ShouldPreserveRetrievalAndConversationIdentity()
    {
        var started = CreateStarted("retrieval-1");
        var completed = CreateNoContextCompleted("retrieval-1");
        var anotherRetrieval = CreateStarted("retrieval-2");

        Assert.Equal(started.CorrelationId, completed.CorrelationId);
        Assert.Equal(started.ConversationId, completed.ConversationId);
        Assert.NotEqual(started.CorrelationId, anotherRetrieval.CorrelationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("query")]
    // Ensures an effective query is exposed only when it is genuinely different from the original query.
    public void Started_ShouldOmitEffectiveQuery_WhenItDoesNotDiffer(string? effectiveQuery)
    {
        var started = new RagSearchStarted("retrieval-1", "agent-1", "conversation-1", "docs", "query", effectiveQuery, 20);

        Assert.Null(started.EffectiveQuery);
    }

    [Fact]
    // Ensures selected context preserves each document and chunk identifier as one atomic result.
    public void Completed_ShouldPreserveSelectedDocumentChunkPair()
    {
        var completed = CreateAcceptedCompleted();

        var selected = Assert.Single(completed.SelectedResults);
        Assert.Equal("document-1", selected.DocumentId);
        Assert.Equal("chunk-1", selected.ChunkId);
    }

    [Fact]
    // Ensures a completed payload rejects a selected-result count that differs from the accepted count.
    public void Completed_ShouldRejectSelectedCountMismatch()
    {
        Assert.Throws<ArgumentException>(() => CreateCompleted(
            actualCandidateCount: 1, acceptedCount: 1, rejectedCount: 0, selectedResults: [], rejectedResults: [],
            topRawScore: 0.9, topNormalizedRelevance: 0.95, noContextReason: null));
    }

    [Fact]
    // Ensures a completed payload rejects a rejected-result count that differs from the declared rejected count.
    public void Completed_ShouldRejectRejectedCountMismatch()
    {
        Assert.Throws<ArgumentException>(() => CreateCompleted(
            actualCandidateCount: 1, acceptedCount: 0, rejectedCount: 1, selectedResults: [], rejectedResults: [],
            topRawScore: 0.2, topNormalizedRelevance: 0.6, RagNoContextReason.CandidatesRejected));
    }

    [Fact]
    // Ensures accepted context cannot be combined with a no-context reason.
    public void Completed_ShouldRejectNoContextReason_WhenContextWasAccepted()
    {
        Assert.Throws<ArgumentException>(() => CreateCompleted(
            actualCandidateCount: 1, acceptedCount: 1, rejectedCount: 0,
            selectedResults: [new RagSearchSelectedResult("document-1", "chunk-1")], rejectedResults: [],
            topRawScore: 0.9, topNormalizedRelevance: 0.95, RagNoContextReason.NoResults));
    }

    [Fact]
    // Ensures a successful retrieval without accepted context requires a truthful no-context reason.
    public void Completed_ShouldRequireNoContextReason_WhenNoContextWasAccepted()
    {
        Assert.Throws<ArgumentNullException>(() => CreateCompleted(
            actualCandidateCount: 0, acceptedCount: 0, rejectedCount: 0, selectedResults: [], rejectedResults: [],
            topRawScore: null, topNormalizedRelevance: null, noContextReason: null));
    }

    [Theory]
    [InlineData(0.2, null)]
    [InlineData(null, 0.6)]
    // Ensures top score metadata cannot be supplied when retrieval returned no candidates.
    public void Completed_ShouldRejectTopScores_WhenNoCandidatesExist(double? rawScore, double? relevance)
    {
        Assert.Throws<ArgumentException>(() => CreateCompleted(
            actualCandidateCount: 0, acceptedCount: 0, rejectedCount: 0, selectedResults: [], rejectedResults: [],
            topRawScore: rawScore, topNormalizedRelevance: relevance, RagNoContextReason.NoResults));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 1)]
    [InlineData(0, 1, -1)]
    // Ensures negative actual, accepted, and rejected counts are rejected before a payload is created.
    public void Completed_ShouldRejectNegativeCounts(int actualCount, int acceptedCount, int rejectedCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCompleted(
            actualCount, acceptedCount, rejectedCount, [], [], null, null, RagNoContextReason.NoResults));
    }

    [Fact]
    // Ensures a completed payload rejects a negative retrieval duration.
    public void Completed_ShouldRejectNegativeDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCompleted(
            0, 0, 0, [], [], null, null, RagNoContextReason.NoResults, TimeSpan.FromTicks(-1)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    // Ensures top raw scores remain finite and safe for later client-facing projection.
    public void Completed_ShouldRejectNonFiniteTopRawScore(double rawScore)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCompleted(
            1, 0, 1, [], [CreateRejected()], rawScore, null, RagNoContextReason.CandidatesRejected));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    // Ensures normalized relevance remains finite and within the provider-independent range.
    public void Completed_ShouldRejectInvalidNormalizedRelevance(double relevance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCompleted(
            1, 0, 1, [], [CreateRejected()], 0.2, relevance, RagNoContextReason.CandidatesRejected));
    }

    [Fact]
    // Ensures a valid no-context outcome carries no selected results or top scores when no candidates exist.
    public void Completed_ShouldCreateValidNoContextPayload()
    {
        var completed = CreateNoContextCompleted();

        Assert.Equal(RagNoContextReason.NoResults, completed.NoContextReason);
        Assert.Empty(completed.SelectedResults);
        Assert.Null(completed.TopRawScore);
    }

    [Fact]
    // Ensures a valid accepted-context outcome carries structured selection and score metadata.
    public void Completed_ShouldCreateValidAcceptedContextPayload()
    {
        var completed = CreateAcceptedCompleted();

        Assert.Equal(1, completed.AcceptedCount);
        Assert.Null(completed.NoContextReason);
        Assert.Equal(0.95, completed.TopNormalizedRelevance);
    }

    [Fact]
    // Ensures a valid failed payload carries only a provider-independent failure classification and duration.
    public void Failed_ShouldCreateValidFailurePayload()
    {
        var failed = CreateFailed(RetrievalErrorCode.VectorStoreQueryFailed);

        Assert.Equal(RetrievalErrorCode.VectorStoreQueryFailed, failed.FailureClassification);
        Assert.Equal(TimeSpan.FromMilliseconds(8), failed.Duration);
        Assert.DoesNotContain(typeof(RagSearchFailed).GetProperties(), property => typeof(Exception).IsAssignableFrom(property.PropertyType));
    }

    [Fact]
    // Ensures failed payloads reject successful, undefined, and negative-duration failure states.
    public void Failed_ShouldRejectInvalidFailureStates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateFailed(RetrievalErrorCode.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateFailed((RetrievalErrorCode)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateFailed(RetrievalErrorCode.RetrievalFailed, TimeSpan.FromTicks(-1)));
    }

    [Fact]
    // Ensures RAG payload properties, constructors, base types, and generic arguments have no direct or indirect tool dependency.
    public void Contracts_ShouldRemainIndependentFromToolExecutionModels()
    {
        var contractTypes = new[]
        {
            typeof(RagSearchEvent), typeof(RagSearchStarted), typeof(RagSearchCompleted),
            typeof(RagSearchFailed), typeof(RagSearchSelectedResult), typeof(RagSearchRejectedResult),
        };
        var exposedTypes = contractTypes
            .SelectMany(type => type.GetProperties().Select(property => property.PropertyType)
                .Concat(type.GetConstructors().SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)))
                .Append(type.BaseType!))
            .SelectMany(FlattenType)
            .Where(type => type is not null)
            .ToArray();

        Assert.DoesNotContain(exposedTypes, type =>
            type.Name.Contains("Tool", StringComparison.Ordinal) ||
            (type.Namespace?.Contains(".Tools", StringComparison.Ordinal) ?? false));
    }

    private static IEnumerable<Type> FlattenType(Type type)
    {
        yield return type;
        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in FlattenType(argument)) yield return nested;
        }
    }

    private static RagSearchStarted CreateStarted(string correlationId = "retrieval-1") =>
        new(correlationId, "agent-1", "conversation-1", "docs", "query", null, 20);

    private static RagSearchBlocked CreateBlocked(RagIndexReadiness? readiness, RagReadinessSuggestedAction action) =>
        new("correlation", "agent", "conversation", "index", null, null, 1, readiness, "blocked", action);

    private static RagSearchCompleted CreateNoContextCompleted(string correlationId = "retrieval-1") =>
        CreateCompleted(0, 0, 0, [], [], null, null, RagNoContextReason.NoResults, correlationId: correlationId);

    private static RagSearchCompleted CreateAcceptedCompleted() => CreateCompleted(
        1, 1, 0, [new RagSearchSelectedResult("document-1", "chunk-1")], [], 0.9, 0.95, null);

    private static RagSearchCompleted CreateCompleted(
        int actualCandidateCount, int acceptedCount, int rejectedCount,
        IReadOnlyList<RagSearchSelectedResult> selectedResults, IReadOnlyList<RagSearchRejectedResult> rejectedResults,
        double? topRawScore, double? topNormalizedRelevance, RagNoContextReason? noContextReason,
        TimeSpan? duration = null, string correlationId = "retrieval-1") =>
        new(correlationId, "agent-1", "conversation-1", "docs", "query", null, 20,
            actualCandidateCount, acceptedCount, rejectedCount, selectedResults, rejectedResults, 5,
            duration ?? TimeSpan.FromMilliseconds(10), topRawScore, topNormalizedRelevance, noContextReason);

    private static RagSearchRejectedResult CreateRejected() =>
        new("document-2", "chunk-2", 0.2, 0.6, RagResultRejectionReason.BelowMinimumRelevance);

    private static RagSearchFailed CreateFailed(RetrievalErrorCode classification, TimeSpan? duration = null) =>
        new("retrieval-1", "agent-1", "conversation-1", "docs", "query", null, 20,
            classification, duration ?? TimeSpan.FromMilliseconds(8));
}
