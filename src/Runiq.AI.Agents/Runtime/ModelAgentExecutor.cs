using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.Metadata;
using Runiq.AI.Core.Providers;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Reranking;
using Runiq.AI.Rag.Models.Reranking;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.Tools;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Runtime;

namespace Runiq.AI.Agents.Runtime;

/// <summary>Executes the existing model, RAG and tool pipeline through Core chat contracts.</summary>
internal sealed class ModelAgentExecutor : IAgentExecutor
{
    /// <inheritdoc />
    public bool SupportsMemoryFoundation => true;
    private const string NoContextMessage = "No relevant information was found in the configured documents.";

    private readonly IChatClientResolver chatClientResolver;
    private readonly IRagRetriever? ragRetriever;
    private readonly RagObservabilityProjection observability;
    private readonly IRagIndexRegistry? ragIndexRegistry;
    private readonly IRagIngestionManager? ragIngestionManager;
    private readonly IRagReranker? ragReranker;
    private readonly ILogger<ModelAgentExecutor> logger;

    /// <summary>Initializes the model pipeline from scoped host services.</summary>
    /// <param name="chatClientResolver">Resolves provider-neutral model clients.</param>
    /// <param name="observability">Projects safe RAG event metadata.</param>
    /// <param name="ragRetriever">The optional retrieval service.</param>
    /// <param name="ragIndexRegistry">The optional registered RAG indexes.</param>
    /// <param name="ragIngestionManager">The optional index readiness service.</param>
    /// <param name="ragReranker">The optional result reranker.</param>
    /// <param name="logger">The optional server logger for model pipeline diagnostics.</param>
    public ModelAgentExecutor(IChatClientResolver chatClientResolver,
        RagObservabilityProjection observability, IRagRetriever? ragRetriever = null,
        IRagIndexRegistry? ragIndexRegistry = null, IRagIngestionManager? ragIngestionManager = null,
        IRagReranker? ragReranker = null, ILogger<ModelAgentExecutor>? logger = null)
    {
        this.chatClientResolver = chatClientResolver ?? throw new ArgumentNullException(nameof(chatClientResolver));
        this.ragRetriever = ragRetriever;
        this.observability = observability ?? throw new ArgumentNullException(nameof(observability));
        this.ragIndexRegistry = ragIndexRegistry;
        this.ragIngestionManager = ragIngestionManager;
        this.ragReranker = ragReranker;
        this.logger = logger ?? NullLogger<ModelAgentExecutor>.Instance;
    }

    /// <inheritdoc />
    public AgentExecutorKind Kind => AgentExecutorKind.Model;

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(
        AgentExecutionRequest request,
        AgentRunContext run,
        AgentToolInvoker toolInvoker,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var agent = request.Agent;
        var query = request.Query;

        if (query.ProviderSessionId is not null)
        {
            yield return AgentExecutionEvent.Failed("The model executor does not support provider session continuation.",
                "AgentSessionNotSupported");
            yield break;
        }

        var runtimeContext = new AgentRuntimeContext();
        Func<AgentRuntimeContext, RagSearchCompleted>? completeRetrieval = null;

        AgentExecutionEvent? ragConfigurationFailure = null;
        if (agent.Rag is { Enabled: true } ragOptions)
        {
            try
            {
                AgentRagPolicyValidator.Validate(ragOptions, requireIndex: false);
            }
            catch (ArgumentException exception)
            {
                logger.LogWarning(exception, "RAG configuration failed for run {RunId} and agent {AgentId}.", run.RunId, run.AgentId);
                ragConfigurationFailure = AgentExecutionEvent.Failed(
                    $"RAG configuration is invalid for agent '{agent.Id}'; the model was not invoked.",
                    "RagConfigurationInvalid");
            }
        }

        if (ragConfigurationFailure is not null)
        {
            yield return ragConfigurationFailure;
            yield break;
        }

        if (agent.Rag is { Enabled: true } activeRag)
        {
            var indexName = !string.IsNullOrWhiteSpace(query.IndexName)
                ? query.IndexName.Trim()
                : activeRag.IndexName?.Trim();
            if (string.IsNullOrWhiteSpace(indexName))
            {
                yield return AgentExecutionEvent.Failed(
                    $"RAG is enabled for agent '{agent.Id}', but no index was configured; the model was not invoked.",
                    "RagConfigurationInvalid",
                    CreateRagMetadata(activeRag, runtimeContext, modelInvocationSkipped: true, noContextBehaviorApplied: false));
                yield break;
            }

            var correlationId = Guid.NewGuid().ToString("N");
            var safeQueries = observability.ProjectQueries(query.Message, effective: null);
            yield return AgentExecutionEvent.FromRagSearch(new RagSearchStarted(
                correlationId, agent.Id, run.RunId, indexName, safeQueries.Original, safeQueries.Effective,
                activeRag.Acceptance.CandidateCount, activeRag.RetrievalMode));

            cancellationToken.ThrowIfCancellationRequested();
            var readinessBlock = ResolveReadinessBlock(
                correlationId, agent.Id, run.RunId, indexName, safeQueries,
                activeRag.Acceptance.CandidateCount, out var readinessStatus);
            if (readinessBlock is not null)
            {
                yield return AgentExecutionEvent.FromRagSearch(readinessBlock);
                yield return AgentExecutionEvent.Failed(
                    $"RAG index '{indexName}' is not ready; retrieval and model execution were not invoked.",
                    "RagIndexNotReady",
                    CreateRagMetadata(activeRag, runtimeContext, modelInvocationSkipped: true, noContextBehaviorApplied: false));
                yield break;
            }

            if (ragRetriever is null)
            {
                yield return AgentExecutionEvent.FromRagSearch(new RagSearchFailed(
                    correlationId, agent.Id, run.RunId, indexName, safeQueries.Original, safeQueries.Effective,
                    activeRag.Acceptance.CandidateCount, RetrievalErrorCode.InvalidRequest, TimeSpan.Zero));
                yield return AgentExecutionEvent.Failed(
                    $"RAG is enabled for agent '{agent.Id}', but the retrieval service is unavailable; the model was not invoked.",
                    "RagConfigurationInvalid",
                    CreateRagMetadata(activeRag, runtimeContext, modelInvocationSkipped: true, noContextBehaviorApplied: false));
                yield break;
            }

            var retrievalStopwatch = Stopwatch.StartNew();
            Exception? retrievalFailure = null;
            var rerankingBlocksExecution = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                runtimeContext = await SearchRagContextAsync(
                    activeRag, indexName, query.Message, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var acceptedResults = runtimeContext.AcceptedRagResults;
                var reranking = await RagRerankingProcessor.ExecuteAsync(
                    query.Message, acceptedResults, activeRag.Reranking, ragReranker,
                    cancellationToken);
                rerankingBlocksExecution = reranking.BlocksExecution;
                var answerabilityBlocksContext =
                    reranking.OrderedResults.Count > 0 &&
                    activeRag.Mode is RagExecutionMode.Grounded or RagExecutionMode.Required &&
                    reranking.Metadata.Outcome == RagRerankingOutcome.Succeeded &&
                    reranking.Metadata.Answerability != RagAnswerability.Answerable;
                runtimeContext = new AgentRuntimeContext(
                    rerankingBlocksExecution || answerabilityBlocksContext ? [] : reranking.OrderedResults,
                    runtimeContext.RetrievedRagCandidates,
                    runtimeContext.RejectedRagCandidates,
                    rerankingBlocksExecution
                        ? RagNoContextReason.RerankingFailed
                        : answerabilityBlocksContext
                            ? RagNoContextReason.NotAnswerable
                            : runtimeContext.NoContextReason,
                    runtimeContext.RetrievalStatistics,
                    rerankingBlocksExecution ? acceptedResults : reranking.OrderedResults,
                    contextExcludedResults: rerankingBlocksExecution || answerabilityBlocksContext
                        ? acceptedResults.Select(result => new RagContextExcludedResult(
                            result,
                            rerankingBlocksExecution
                                ? RagContextSelectionExclusionReason.RerankingFailed
                                : RagContextSelectionExclusionReason.NotAnswerable,
                            ContextTokenEstimator.EstimateText(result.Chunk.Content))).ToArray()
                        : null,
                    reranking: reranking.Metadata);
                runtimeContext = runtimeContext with { RetrievalCorrelationId = correlationId };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "RAG retrieval failed for run {RunId} and agent {AgentId}.", run.RunId, run.AgentId);
                retrievalFailure = exception;
            }

            retrievalStopwatch.Stop();
            if (retrievalFailure is not null)
            {
                yield return AgentExecutionEvent.FromRagSearch(new RagSearchFailed(
                    correlationId, agent.Id, run.RunId, indexName, safeQueries.Original, safeQueries.Effective,
                    activeRag.Acceptance.CandidateCount, ClassifyRetrievalFailure(retrievalFailure),
                    retrievalStopwatch.Elapsed));
                yield return AgentExecutionEvent.Failed(
                    $"RAG retrieval failed for agent '{agent.Id}'; the model was not invoked.",
                    "RagRetrievalFailed",
                    CreateRagMetadata(activeRag, runtimeContext, modelInvocationSkipped: true, noContextBehaviorApplied: false));
                yield break;
            }

            if (rerankingBlocksExecution)
            {
                yield return AgentExecutionEvent.FromRagSearch(CreateRagSearchCompleted(
                    correlationId, run.RunId, agent, activeRag, indexName, safeQueries, runtimeContext,
                    retrievalStopwatch.Elapsed, readinessStatus));
                yield return AgentExecutionEvent.Failed(
                    $"RAG reranking failed for agent '{agent.Id}'; the model was not invoked.",
                    "RagRerankingFailed",
                    CreateRagMetadata(activeRag, runtimeContext, modelInvocationSkipped: true, noContextBehaviorApplied: false));
                yield break;
            }

            completeRetrieval = context => CreateRagSearchCompleted(
                correlationId, run.RunId, agent, activeRag, indexName, safeQueries, context,
                retrievalStopwatch.Elapsed, readinessStatus);
        }

        // Resolve the configured named-model override once so every request, including tool continuations,
        // uses the same Core model identity before capability validation occurs in the chat client boundary.
        var effectiveModel = ProviderModelReferenceResolver.Resolve(agent.ModelReference, agent.Provider);

        var endpoint = ProviderDefaults.ResolveUrl(
            agent.ProviderName,
            agent.Id,
            agent.Provider?.Url);
        var activeTurn = new List<ChatMessage> { new(ChatRole.User, query.Message) };
        var tools = agent.Tools.Select(MapToolDefinition).ToArray();
        var retrievalContext = runtimeContext;
        Dictionary<(string Document, string Chunk), int>? citationNumbers = null;
        var invocation = 0;
        IReadOnlyList<AgentCitation> citations = [];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assembly = AgentContextAssembler.Assemble(agent, request.History, activeTurn, tools,
                retrievalContext, ++invocation, citationNumbers);
            runtimeContext = assembly.Context;
            var budget = assembly.Budget;
            logger.LogInformation("Context assembly for run {RunId}, invocation {Invocation}: {AccountingMode}, window {Window}, reserve {Reserve}, mandatory {Mandatory}, evidence {Evidence}, history {History}, excluded history {ExcludedHistory}, excluded evidence {ExcludedEvidence}, overflow {Overflow}.",
                run.RunId, invocation, budget.AccountingMode, budget.MaximumContextTokens, budget.ResponseTokenReserve,
                budget.MandatoryPromptTokens, budget.SelectedEvidenceTokens, budget.SelectedHistoryTokens,
                budget.ExcludedHistoryMessages, budget.ExcludedEvidenceChunks, budget.MandatoryPromptOverflow);
            if (invocation == 1 && completeRetrieval is not null)
                yield return AgentExecutionEvent.FromRagSearch(completeRetrieval(runtimeContext)) with { ContextBudget = budget };
            if (budget.MandatoryPromptOverflow)
            {
                yield return AgentExecutionEvent.Failed(
                    $"Estimated mandatory prompt ({budget.MandatoryPromptTokens}) plus response reserve ({budget.ResponseTokenReserve}) exceeds context window ({budget.MaximumContextTokens}); model invocation {invocation} was not performed.",
                    agent.Rag?.Enabled == true ? "RagContextBudgetExceeded" : "ContextBudgetExceeded",
                    CreateRagMetadata(agent.Rag, runtimeContext, true, false)) with { ContextBudget = budget };
                yield break;
            }
            if (agent.Rag is { Enabled: true } policy && !runtimeContext.HasContext)
            {
                if (policy.NoContextBehavior == RagNoContextBehavior.ReturnNotFound)
                {
                    yield return AgentExecutionEvent.AssistantDelta(NoContextMessage) with { ContextBudget = budget };
                    yield return AgentExecutionEvent.Completed(CreateRagMetadata(policy, runtimeContext, true, true)) with { ContextBudget = budget };
                    yield break;
                }
                if (policy.NoContextBehavior == RagNoContextBehavior.FailExecution)
                {
                    yield return AgentExecutionEvent.Failed(NoContextMessage, "RagContextUnavailable",
                        CreateRagMetadata(policy, runtimeContext, true, true)) with { ContextBudget = budget };
                    yield break;
                }
            }
            if (ValidateProviderRuntime(agent) is { } validationFailure)
            {
                yield return AgentExecutionEvent.Failed(validationFailure.ErrorMessage!, validationFailure.ErrorCode,
                    CreateRagMetadata(agent.Rag, runtimeContext, true, agent.Rag?.Enabled == true && !runtimeContext.HasContext))
                    with { ContextBudget = budget };
                yield break;
            }
            // Freeze source identities after the first selection. Excluded sources never inherit another source's number.
            if (citationNumbers is null)
            {
                citationNumbers = new();
                foreach (var source in runtimeContext.RetrievedRagContext.Concat(retrievalContext.RetrievedRagContext))
                    citationNumbers.TryAdd((source.Chunk.DocumentId, source.Chunk.Id), citationNumbers.Count + 1);
                runtimeContext = runtimeContext with { CitationNumbers = citationNumbers };
            }
            var options = new ChatRequestOptions
            {
                ReasoningEffort = agent.ReasoningEffort,
                Verbosity = agent.Verbosity
            };
            // Send only this explicitly budgeted projection; a server-side response chain would restore excluded content.

            var chatRequest = new ChatRequest(
                effectiveModel,
                assembly.Messages,
                endpoint,
                agent.ApiKey,
                tools,
                Options: options);
            cancellationToken.ThrowIfCancellationRequested();
            var client = chatClientResolver.Resolve(chatRequest);
            var toolCalls = new List<ChatToolCall>();
            var roundResponse = new StringBuilder();
            ChatContinuation? continuation = null;

            cancellationToken.ThrowIfCancellationRequested();
            run.ContextBudget = budget;
            await foreach (var update in client.CompleteStreamingAsync(chatRequest, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                continuation = update.Continuation ?? continuation;
                if (update.Kind == ChatStreamingUpdateKind.ContentDelta && !string.IsNullOrEmpty(update.ContentDelta))
                {
                    roundResponse.Append(update.ContentDelta);
                    yield return AgentExecutionEvent.AssistantDelta(update.ContentDelta) with { ContextBudget = budget };
                }
                else if (update.Kind == ChatStreamingUpdateKind.ToolCallDelta && update.ToolCall is not null)
                {
                    toolCalls.Add(update.ToolCall);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            citations = AgentCitationProcessor.Validate(roundResponse.ToString(), runtimeContext, citations);
            if (toolCalls.Count == 0)
            {
                yield return AgentExecutionEvent.Completed(
                    CreateRagMetadata(
                        agent.Rag,
                        runtimeContext,
                        modelInvocationSkipped: false,
                        noContextBehaviorApplied: agent.Rag?.Enabled == true && !runtimeContext.HasContext),
                    citations) with { ContextBudget = budget };
                yield break;
            }

            if (request.Turn is not null)
                await request.Turn.AppendToolCallsAsync(toolCalls, cancellationToken);
            activeTurn.Add(new ChatMessage(ChatRole.Assistant, roundResponse.ToString(), ToolCalls: toolCalls)
            {
                Continuation = continuation
            });
            foreach (var toolCall in toolCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return AgentExecutionEvent.ToolCallStarted(toolCall.Id, toolCall.Name, toolCall.ArgumentsJson) with { ContextBudget = budget };
                cancellationToken.ThrowIfCancellationRequested();
                var result = await toolInvoker!.InvokeAsync(agent, toolCall.Name, toolCall.ArgumentsJson, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                string output;
                if (result.IsSuccess)
                {
                    output = string.IsNullOrWhiteSpace(result.OutputJson) ? "{}" : result.OutputJson;
                }
                else
                {
                    output = JsonSerializer.Serialize(new
                    {
                        isSuccess = false,
                        errorCode = result.ErrorCode ?? "ToolExecutionFailed",
                        errorMessage = result.ErrorMessage ?? "Tool execution failed."
                    });
                }
                if (request.Turn is not null)
                    await request.Turn.AppendToolResultAsync(toolCall.Id, output, cancellationToken);
                if (result.IsSuccess)
                    yield return AgentExecutionEvent.ToolCallCompleted(toolCall.Id, toolCall.Name, output);
                else
                    yield return AgentExecutionEvent.ToolCallFailed(toolCall.Id, toolCall.Name,
                        result.ErrorMessage ?? "Tool execution failed.", result.ErrorCode);
                activeTurn.Add(new ChatMessage(ChatRole.Tool, output, toolCall.Id));
            }
        }
    }

    private RagSearchBlocked? ResolveReadinessBlock(string correlationId, string agentId, string conversationId,
        string indexName, (string? Original, string? Effective) safeQueries, int requestedCandidateCount,
        out RagIndexRuntimeStatus? readinessStatus)
    {
        readinessStatus = null;
        if (ragIndexRegistry is null || ragIngestionManager is null) return null;
        if (!ragIndexRegistry.Registrations.Any(item => string.Equals(item.Name, indexName, StringComparison.Ordinal)))
        {
            return new RagSearchBlocked(correlationId, agentId, conversationId, indexName, safeQueries.Original,
                safeQueries.Effective, requestedCandidateCount, null, "IndexNotRegistered",
                RagReadinessSuggestedAction.CheckConfiguration);
        }

        var status = ragIngestionManager.GetStatus(indexName);
        readinessStatus = status;
        if (status.Readiness is RagIndexReadiness.Ready or RagIndexReadiness.Degraded) return null;
        var action = status.Readiness switch
        {
            RagIndexReadiness.NotInitialized => RagReadinessSuggestedAction.StartIngestion,
            RagIndexReadiness.Initializing => RagReadinessSuggestedAction.WaitForIngestion,
            RagIndexReadiness.Failed => RagReadinessSuggestedAction.RetryIngestion,
            _ => throw new ArgumentOutOfRangeException(nameof(status.Readiness), status.Readiness, "Unsupported RAG readiness state.")
        };
        var operation = status.ActiveOperation;
        var failure = status.LastOperation?.Progress.LastFailure?.Message;
        return new RagSearchBlocked(correlationId, agentId, conversationId, indexName, safeQueries.Original,
            safeQueries.Effective, requestedCandidateCount, status.Readiness, status.Readiness.ToString(), action,
            status.LastUpdatedAt, operation?.State, operation?.Reason,
            operation is null ? null : new RagReadinessProgress(operation.Progress.DiscoveredDocuments,
                operation.Progress.ProcessedDocuments, operation.Progress.FailedDocuments), failure);
    }

    private static ChatToolDefinition MapToolDefinition(AgentToolRegistration tool) => new(
        tool.Name,
        string.IsNullOrWhiteSpace(tool.Description) ? $"Executes the {tool.Name} tool." : tool.Description,
        JsonSerializer.Serialize(ToolJsonSchemaGenerator.CreateSchema(tool.InputType)));

    /// <summary>
    /// Provider runtime ayarlarinin Ã§alistirma Ã¶ncesi geÃ§erli olup olmadigini dogrular.
    /// </summary>
    /// <param name="agent">Dogrulanacak agent tanimidir.</param>
    /// <returns>GeÃ§ersiz ayar varsa hata sonucudur; aksi halde null dÃ¶ner.</returns>
    private static AgentExecutionResult? ValidateProviderRuntime(Agent agent)
    {
        var providerDefault = ProviderDefaults.Get(agent.ProviderName);
        var hasCustomUrl = !string.IsNullOrWhiteSpace(agent.Provider?.Url);

        if (providerDefault.RequiresApiKey &&
            !hasCustomUrl &&
            string.IsNullOrWhiteSpace(agent.ApiKey))
        {
            return AgentExecutionResult.Failure(
                errorCode: "ApiKeyMissing",
                errorMessage: $"Agent '{agent.Id}' uses default provider endpoint for '{agent.ProviderName}' but ApiKey is missing.");
        }

        return null;
    }

    /// <summary>
    /// Agent RAG yapilandirmasi varsa RAG retrieval Ã§alistirir ve sonuÃ§lari runtime context'e ekler.
    /// </summary>
    private async Task<AgentRuntimeContext> SearchRagContextAsync(
        AgentRagOptions ragOptions,
        string indexName,
        string query,
        CancellationToken cancellationToken)
    {
        var retrieval = await ragRetriever!.RetrieveWithMetadataAsync(
            new RagQuery
            {
                Text = query,
                IndexName = indexName,
                TopK = ragOptions.Acceptance.CandidateCount,
                Mode = ragOptions.RetrievalMode,
            },
            cancellationToken).ConfigureAwait(false);
        var candidates = retrieval.Candidates;

        if (candidates is null)
        {
            throw new RagRetrievalExecutionException("The RAG retriever returned a null result collection.");
        }

        var evaluation = RagResultAcceptanceEvaluator.Evaluate(candidates, ragOptions.Acceptance);
        RagNoContextReason? noContextReason = evaluation.AcceptedResults.Count > 0
            ? null
            : candidates.Count == 0
                ? RagNoContextReason.NoResults
                : evaluation.RejectedResults.All(
                    result => result.Reason == RagResultRejectionReason.BelowMinimumRelevance)
                    ? RagNoContextReason.BelowRelevanceThreshold
                    : RagNoContextReason.CandidatesRejected;

        return new AgentRuntimeContext(
            evaluation.AcceptedResults,
            evaluation.Candidates,
            evaluation.RejectedResults,
            noContextReason,
            retrieval.Statistics);
    }

    private static RetrievalErrorCode ClassifyRetrievalFailure(Exception exception) =>
        exception is RagRetrievalExecutionException retrievalException
            ? retrievalException.ErrorCode
            : RetrievalErrorCode.RetrievalFailed;

    private RagSearchCompleted CreateRagSearchCompleted(
        string correlationId,
        string conversationId,
        Agent agent,
        AgentRagOptions options,
        string indexName,
        (string? Original, string? Effective) queries,
        AgentRuntimeContext runtimeContext,
        TimeSpan duration,
        RagIndexRuntimeStatus? readinessStatus)
    {
        var topCandidate = runtimeContext.RetrievedRagCandidates.FirstOrDefault(candidate =>
            candidate is not null &&
            candidate.RawScore is double rawScore && double.IsFinite(rawScore) &&
            !string.IsNullOrWhiteSpace(candidate.Metric) &&
            (candidate.Relevance is null ||
                double.IsFinite(candidate.Relevance.Value) && candidate.Relevance.Value is >= 0 and <= 1));

        return new RagSearchCompleted(
            correlationId,
            agent.Id,
            conversationId,
            indexName,
            queries.Original,
            queries.Effective,
            options.Acceptance.CandidateCount,
            runtimeContext.RetrievedRagCandidates.Count,
            runtimeContext.AcceptedRagResults.Count,
            runtimeContext.RejectedRagCandidates.Count,
            runtimeContext.RetrievedRagContext
                .Select(result =>
                {
                    var preview = observability.ProjectContent(result.Chunk.Content, selected: true);
                    return new RagSearchSelectedResult(result.Chunk.DocumentId, result.Chunk.Id,
                        result.RawScore, result.Relevance, result.Metric, result.HigherIsBetter,
                        preview.Value, preview.Truncated,
                        observability.ProjectMetadata(new Dictionary<string, string>(result.Metadata.Values)),
                        result.Provenance);
                })
                .ToArray(),
            runtimeContext.RejectedRagCandidates
                .Select(result =>
                {
                    var preview = observability.ProjectContent(result.Result.Chunk.Content, selected: false);
                    return new RagSearchRejectedResult(result.Result.Chunk.DocumentId, result.Result.Chunk.Id,
                        result.Result.RawScore, result.Result.Relevance, result.Reason,
                        preview.Value, preview.Truncated,
                        observability.ProjectMetadata(new Dictionary<string, string>(result.Result.Metadata.Values)),
                        result.Result.Provenance);
                })
                .ToArray(),
            options.Acceptance.MaximumAcceptedResults,
            duration,
            topCandidate?.RawScore,
            topCandidate?.Relevance,
            runtimeContext.NoContextReason,
            readinessStatus?.Readiness == RagIndexReadiness.Degraded ? RagIndexReadiness.Degraded : null,
            readinessStatus?.Readiness == RagIndexReadiness.Degraded ? readinessStatus.LastOperation?.Progress.LastFailure?.Message : null,
            options.RetrievalMode,
            runtimeContext.RetrievalStatistics.SemanticCandidateCount,
            runtimeContext.RetrievalStatistics.LexicalCandidateCount,
            runtimeContext.RetrievalStatistics.FusedCandidateCount,
            runtimeContext.ContextExcludedResults.Select(result =>
                new RagSearchContextExcludedResult(
                    result.Result.Chunk.DocumentId,
                    result.Result.Chunk.Id,
                    result.Reason,
                    result.EstimatedTokens,
                    result.Result.Provenance)).ToArray(),
            runtimeContext.ContextBudget,
            runtimeContext.Reranking);
    }

    private static AgentRagExecutionMetadata? CreateRagMetadata(
        AgentRagOptions? options,
        AgentRuntimeContext runtimeContext,
        bool modelInvocationSkipped,
        bool noContextBehaviorApplied)
    {
        if (options is null || !options.Enabled)
        {
            return null;
        }

        return new AgentRagExecutionMetadata(
            options.Mode,
            runtimeContext.AcceptedRagResults.Count > 0,
            noContextBehaviorApplied ? options.NoContextBehavior : null,
            runtimeContext.NoContextReason,
            modelInvocationSkipped,
            !modelInvocationSkipped &&
                runtimeContext.HasContext &&
                options.Mode is RagExecutionMode.Grounded or RagExecutionMode.Required,
            runtimeContext.RetrievedRagCandidates,
            runtimeContext.AcceptedRagResults,
            runtimeContext.RejectedRagCandidates,
            options.RetrievalMode,
            runtimeContext.RetrievalStatistics,
            runtimeContext.RetrievedRagContext,
            runtimeContext.ContextExcludedResults,
            runtimeContext.ContextBudget,
            runtimeContext.Reranking);
    }

}
