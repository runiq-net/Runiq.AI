using Runiq.AI.Agents.Configuration;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Agents;

/// <summary>Provides the common identity and request data for a RAG search lifecycle payload.</summary>
public abstract class RagSearchEvent
{
    /// <summary>Initializes the common data for a RAG search lifecycle payload.</summary>
    /// <param name="correlationId">The identifier shared by payloads from the same retrieval.</param>
    /// <param name="agentId">The identifier of the agent that initiated the retrieval.</param>
    /// <param name="conversationId">The identifier of the conversation that contains the retrieval.</param>
    /// <param name="indexName">The effective index queried by the runtime.</param>
    /// <param name="originalQuery">The original query received by the runtime.</param>
    /// <param name="effectiveQuery">The effective query when it differs from the original query.</param>
    /// <param name="requestedCandidateCount">The maximum number of candidates requested from retrieval.</param>
    protected RagSearchEvent(string correlationId, string agentId, string conversationId, string indexName,
        string originalQuery, string? effectiveQuery, int requestedCandidateCount)
    {
        CorrelationId = RequireValue(correlationId, nameof(correlationId));
        AgentId = RequireValue(agentId, nameof(agentId));
        ConversationId = RequireValue(conversationId, nameof(conversationId));
        IndexName = RequireValue(indexName, nameof(indexName));
        OriginalQuery = RequireValue(originalQuery, nameof(originalQuery));
        EffectiveQuery = string.IsNullOrWhiteSpace(effectiveQuery) || string.Equals(originalQuery, effectiveQuery, StringComparison.Ordinal)
            ? null
            : effectiveQuery;
        RequestedCandidateCount = RequirePositive(requestedCandidateCount, nameof(requestedCandidateCount));
    }

    /// <summary>Gets the identifier shared by payloads from the same retrieval.</summary>
    public string CorrelationId { get; }
    /// <summary>Gets the identifier of the agent that initiated the retrieval.</summary>
    public string AgentId { get; }
    /// <summary>Gets the identifier of the conversation that contains the retrieval.</summary>
    public string ConversationId { get; }
    /// <summary>Gets the effective index queried by the runtime.</summary>
    public string IndexName { get; }
    /// <summary>Gets the original query received by the runtime.</summary>
    public string OriginalQuery { get; }
    /// <summary>Gets the effective query when it differs from <see cref="OriginalQuery"/>; otherwise, gets <see langword="null"/>.</summary>
    public string? EffectiveQuery { get; }
    /// <summary>Gets the maximum number of candidates requested from retrieval.</summary>
    public int RequestedCandidateCount { get; }

    private static string RequireValue(string value, string parameterName) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("The value cannot be null, empty, or whitespace.", parameterName)
        : value;
    internal static int RequireNonNegative(int value, string parameterName) => value < 0
        ? throw new ArgumentOutOfRangeException(parameterName, value, "The value cannot be negative.") : value;
    internal static int RequirePositive(int value, string parameterName) => value <= 0
        ? throw new ArgumentOutOfRangeException(parameterName, value, "The value must be greater than zero.") : value;
}

/// <summary>Indicates that a RAG retrieval operation has started.</summary>
public sealed class RagSearchStarted : RagSearchEvent
{
    /// <summary>Initializes a RAG retrieval started payload.</summary>
    /// <inheritdoc cref="RagSearchEvent(string, string, string, string, string, string?, int)"/>
    public RagSearchStarted(string correlationId, string agentId, string conversationId, string indexName,
        string originalQuery, string? effectiveQuery, int requestedCandidateCount)
        : base(correlationId, agentId, conversationId, indexName, originalQuery, effectiveQuery, requestedCandidateCount) { }
}

/// <summary>Indicates that a RAG retrieval operation completed successfully, with or without accepted context.</summary>
public sealed class RagSearchCompleted : RagSearchEvent
{
    /// <summary>Initializes a RAG retrieval completed payload.</summary>
    /// <param name="correlationId">The identifier shared by payloads from the same retrieval.</param>
    /// <param name="agentId">The identifier of the agent that initiated the retrieval.</param>
    /// <param name="conversationId">The identifier of the conversation that contains the retrieval.</param>
    /// <param name="indexName">The effective index queried by the runtime.</param>
    /// <param name="originalQuery">The original query received by the runtime.</param>
    /// <param name="effectiveQuery">The effective query when it differs from the original query.</param>
    /// <param name="requestedCandidateCount">The maximum number of candidates requested from retrieval.</param>
    /// <param name="actualCandidateCount">The number of candidates returned by retrieval.</param>
    /// <param name="acceptedCount">The number of candidates accepted as context.</param>
    /// <param name="rejectedCount">The number of candidates rejected by runtime policy.</param>
    /// <param name="selectedResults">The document and chunk pairs selected as context.</param>
    /// <param name="rejectedResults">The structured candidates rejected by runtime policy.</param>
    /// <param name="maximumAcceptedResultCount">The configured maximum number of accepted results.</param>
    /// <param name="duration">The elapsed retrieval duration.</param>
    /// <param name="topRawScore">The finite raw score of the highest-ranked valid candidate, when available.</param>
    /// <param name="topNormalizedRelevance">The normalized relevance of the highest-ranked candidate, when reliably available.</param>
    /// <param name="noContextReason">The verifiable reason no context was accepted, or null when context was accepted.</param>
    public RagSearchCompleted(string correlationId, string agentId, string conversationId, string indexName,
        string originalQuery, string? effectiveQuery, int requestedCandidateCount, int actualCandidateCount,
        int acceptedCount, int rejectedCount, IReadOnlyList<RagSearchSelectedResult> selectedResults,
        IReadOnlyList<RagSearchRejectedResult> rejectedResults, int maximumAcceptedResultCount, TimeSpan duration,
        double? topRawScore, double? topNormalizedRelevance, RagNoContextReason? noContextReason)
        : base(correlationId, agentId, conversationId, indexName, originalQuery, effectiveQuery, requestedCandidateCount)
    {
        ActualCandidateCount = RequireNonNegative(actualCandidateCount, nameof(actualCandidateCount));
        AcceptedCount = RequireNonNegative(acceptedCount, nameof(acceptedCount));
        RejectedCount = RequireNonNegative(rejectedCount, nameof(rejectedCount));
        SelectedResults = selectedResults ?? throw new ArgumentNullException(nameof(selectedResults));
        RejectedResults = rejectedResults ?? throw new ArgumentNullException(nameof(rejectedResults));
        MaximumAcceptedResultCount = RequirePositive(maximumAcceptedResultCount, nameof(maximumAcceptedResultCount));
        Duration = duration < TimeSpan.Zero ? throw new ArgumentOutOfRangeException(nameof(duration)) : duration;
        ValidateCounts();
        ValidateOutcome(noContextReason);
        ValidateScores(topRawScore, topNormalizedRelevance);
        TopRawScore = topRawScore;
        TopNormalizedRelevance = topNormalizedRelevance;
        NoContextReason = noContextReason;
    }

    /// <summary>Gets the number of candidates returned by retrieval.</summary>
    public int ActualCandidateCount { get; }
    /// <summary>Gets the number of candidates accepted as context.</summary>
    public int AcceptedCount { get; }
    /// <summary>Gets the number of candidates rejected by runtime policy.</summary>
    public int RejectedCount { get; }
    /// <summary>Gets the configured maximum number of accepted results.</summary>
    public int MaximumAcceptedResultCount { get; }
    /// <summary>Gets the elapsed retrieval duration.</summary>
    public TimeSpan Duration { get; }
    /// <summary>Gets the finite raw score of the highest-ranked valid candidate, when available.</summary>
    public double? TopRawScore { get; }
    /// <summary>Gets the normalized relevance of the highest-ranked candidate, when reliably available.</summary>
    public double? TopNormalizedRelevance { get; }
    /// <summary>Gets the document and chunk pairs selected as context.</summary>
    public IReadOnlyList<RagSearchSelectedResult> SelectedResults { get; }
    /// <summary>Gets the structured candidates rejected by runtime policy.</summary>
    public IReadOnlyList<RagSearchRejectedResult> RejectedResults { get; }
    /// <summary>Gets the verifiable reason no context was accepted, or null when context was accepted.</summary>
    public RagNoContextReason? NoContextReason { get; }

    private void ValidateCounts()
    {
        if (AcceptedCount + RejectedCount != ActualCandidateCount)
            throw new ArgumentException("Accepted and rejected counts must equal the actual candidate count.");
        if (SelectedResults.Count != AcceptedCount)
            throw new ArgumentException("Selected result count must equal the accepted count.", nameof(SelectedResults));
        if (RejectedResults.Count != RejectedCount)
            throw new ArgumentException("Rejected result count must equal the rejected count.", nameof(RejectedResults));
        if (AcceptedCount > MaximumAcceptedResultCount)
            throw new ArgumentOutOfRangeException(nameof(AcceptedCount), AcceptedCount, "Accepted count cannot exceed the configured maximum.");
    }

    private void ValidateOutcome(RagNoContextReason? noContextReason)
    {
        if (AcceptedCount > 0 && noContextReason is not null)
            throw new ArgumentException("A no-context reason cannot be supplied when context was accepted.", nameof(noContextReason));
        if (AcceptedCount == 0 && noContextReason is null)
            throw new ArgumentNullException(nameof(noContextReason), "A successful retrieval without accepted context requires a no-context reason.");
        if (noContextReason is not null && !Enum.IsDefined(noContextReason.Value))
            throw new ArgumentOutOfRangeException(nameof(noContextReason), noContextReason, "The no-context reason is not defined.");
    }

    private void ValidateScores(double? topRawScore, double? topNormalizedRelevance)
    {
        if (ActualCandidateCount == 0 && (topRawScore is not null || topNormalizedRelevance is not null))
            throw new ArgumentException("Top scores cannot be supplied when retrieval returned no candidates.");
        if (topRawScore is not null && !double.IsFinite(topRawScore.Value))
            throw new ArgumentOutOfRangeException(nameof(topRawScore), topRawScore, "The top raw score must be finite.");
        if (topNormalizedRelevance is not null && (!double.IsFinite(topNormalizedRelevance.Value) || topNormalizedRelevance.Value is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(topNormalizedRelevance), topNormalizedRelevance, "Normalized relevance must be between zero and one.");
    }
}

/// <summary>Indicates that a RAG retrieval operation failed before producing a successful outcome.</summary>
public sealed class RagSearchFailed : RagSearchEvent
{
    /// <summary>Initializes a RAG retrieval failed payload.</summary>
    /// <param name="correlationId">The identifier shared by payloads from the same retrieval.</param>
    /// <param name="agentId">The identifier of the agent that initiated the retrieval.</param>
    /// <param name="conversationId">The identifier of the conversation that contains the retrieval.</param>
    /// <param name="indexName">The effective index queried by the runtime.</param>
    /// <param name="originalQuery">The original query received by the runtime.</param>
    /// <param name="effectiveQuery">The effective query when it differs from the original query.</param>
    /// <param name="requestedCandidateCount">The maximum number of candidates requested from retrieval.</param>
    /// <param name="failureClassification">The provider-independent retrieval failure classification.</param>
    /// <param name="duration">The elapsed duration before retrieval failed.</param>
    public RagSearchFailed(string correlationId, string agentId, string conversationId, string indexName,
        string originalQuery, string? effectiveQuery, int requestedCandidateCount,
        RetrievalErrorCode failureClassification, TimeSpan duration)
        : base(correlationId, agentId, conversationId, indexName, originalQuery, effectiveQuery, requestedCandidateCount)
    {
        if (!Enum.IsDefined(failureClassification) || failureClassification == RetrievalErrorCode.None)
            throw new ArgumentOutOfRangeException(nameof(failureClassification), failureClassification, "A failed payload requires a failure classification.");
        FailureClassification = failureClassification;
        Duration = duration < TimeSpan.Zero ? throw new ArgumentOutOfRangeException(nameof(duration)) : duration;
    }
    /// <summary>Gets the provider-independent retrieval failure classification.</summary>
    public RetrievalErrorCode FailureClassification { get; }
    /// <summary>Gets the elapsed duration before retrieval failed.</summary>
    public TimeSpan Duration { get; }
}
