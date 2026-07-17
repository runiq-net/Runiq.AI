using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.Metadata;
using Runiq.AI.Core.Providers;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.Tools;

namespace Runiq.AI.Agents.Runtime;

/// <summary>
/// Kayitli agent tanimlarini provider pipeline'i Ã¼zerinden Ã§alistiran runtime servisidir.
/// </summary>
public sealed class AgentExecutionRuntime
{
    private const string NoContextMessage = "No relevant information was found in the configured documents.";

    private readonly IEnumerable<Agent> agents;
    private readonly IChatClientResolver chatClientResolver;
    private readonly AgentToolInvoker toolInvoker;
    private readonly IRagRetriever? ragRetriever;

    /// <summary>
    /// Initializes the runtime with two provider-neutral clients for compatibility with existing manual construction.
    /// </summary>
    /// <param name="agents">The registered agents available to the runtime.</param>
    /// <param name="openAIResponsesClient">The client used for native OpenAI requests.</param>
    /// <param name="openAICompatibleClient">The client used for OpenAI-compatible and Ollama requests.</param>
    /// <param name="toolInvoker">The agent-owned tool invoker.</param>
    /// <param name="ragRetriever">Optional RAG retriever.</param>
    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        IChatClient openAIResponsesClient,
        IChatClient openAICompatibleClient,
        AgentToolInvoker toolInvoker,
        IRagRetriever? ragRetriever = null)
        : this(
            agents,
            new FixedChatClientResolver(openAIResponsesClient, openAICompatibleClient),
            toolInvoker,
            ragRetriever)
    {
    }


    /// <summary>
    /// Initializes the agent runtime with provider-neutral model resolution and agent-owned orchestration services.
    /// </summary>
    /// <param name="agents">Runtime tarafindan Ã§alistirilabilecek kayitli agent koleksiyonudur.</param>
    /// <param name="chatClientResolver">Resolves the shared chat client for each agent model.</param>
    /// <param name="toolInvoker">Agent tool Ã§agrilarini Ã§alistiran invoker Ã¶rnegidir.</param>
    /// <param name="ragRetriever">Agent RAG sorgularini Ã§alistiracak opsiyonel retriever servisidir.</param>
    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        IChatClientResolver chatClientResolver,
        AgentToolInvoker toolInvoker,
        IRagRetriever? ragRetriever = null)
    {
        this.agents = agents ?? throw new ArgumentNullException(nameof(agents));
        this.chatClientResolver = chatClientResolver ?? throw new ArgumentNullException(nameof(chatClientResolver));
        this.toolInvoker = toolInvoker ?? throw new ArgumentNullException(nameof(toolInvoker));
        this.ragRetriever = ragRetriever;
    }

    /// <summary>
    /// Agent cevabini agent kimligine gÃ¶re tek seferlik sonuÃ§ olarak Ã¼retir.
    /// </summary>
    /// <param name="agentId">Ã‡alistirilacak agent kimligidir.</param>
    /// <param name="input">Agent'a gÃ¶nderilecek kullanici girdisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alistirma sonucudur.</returns>
    public async Task<AgentExecutionResult> ExecuteAsync(
        string agentId,
        string input,
        CancellationToken cancellationToken = default)
    {
        var agent = FindAgent(agentId);

        if (agent is null)
        {
            return AgentExecutionResult.Failure(
                errorCode: "AgentNotFound",
                errorMessage: $"Agent '{agentId}' was not found.");
        }

        return await ExecuteAgentAsync(
            agent,
            new AgentQuery(input),
            cancellationToken);
    }

    /// <summary>
    /// Agent cevabini agent kimligine gÃ¶re runtime query bilgisiyle tek seferlik sonuÃ§ olarak Ã¼retir.
    /// </summary>
    /// <param name="agentId">Ã‡alistirilacak agent kimligidir.</param>
    /// <param name="query">Agent'a gÃ¶nderilecek runtime query bilgisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alistirma sonucudur.</returns>
    public async Task<AgentExecutionResult> ExecuteAsync(
        string agentId,
        AgentQuery query,
        CancellationToken cancellationToken = default)
    {
        var agent = FindAgent(agentId);

        if (agent is null)
        {
            return AgentExecutionResult.Failure(
                errorCode: "AgentNotFound",
                errorMessage: $"Agent '{agentId}' was not found.");
        }

        return await ExecuteAgentAsync(
            agent,
            query,
            cancellationToken);
    }

    /// <summary>
    /// Kayit listesine bagli olmayan geÃ§ici bir agent tanimiyla tek seferlik sonuÃ§ Ã¼retir.
    /// </summary>
    /// <param name="agent">Ã‡alistirilacak agent tanimidir.</param>
    /// <param name="input">Agent'a gÃ¶nderilecek kullanici girdisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alistirma sonucudur.</returns>
    public Task<AgentExecutionResult> ExecuteAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAgentAsync(
            agent,
            new AgentQuery(input),
            cancellationToken);
    }

    /// <summary>
    /// Kayit listesine bagli olmayan geÃ§ici bir agent tanimiyla runtime query bilgisiyle tek seferlik sonuÃ§ Ã¼retir.
    /// </summary>
    /// <param name="agent">Ã‡alistirilacak agent tanimidir.</param>
    /// <param name="query">Agent'a gÃ¶nderilecek runtime query bilgisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alistirma sonucudur.</returns>
    public Task<AgentExecutionResult> ExecuteAsync(
        Agent agent,
        AgentQuery query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAgentAsync(
            agent,
            query,
            cancellationToken);
    }

    /// <summary>
    /// Agent cevabini agent kimligine gÃ¶re event stream olarak Ã¼retir.
    /// </summary>
    /// <param name="agentId">Ã‡alistirilacak agent kimligidir.</param>
    /// <param name="input">Agent'a gÃ¶nderilecek kullanici girdisidir.</param>
    /// <param name="toolInvoker">Varsa bu Ã§agri iÃ§in kullanilacak tool invoker Ã¶rnegidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alismasi sirasinda Ã¼retilen olay stream'idir.</returns>
    public async IAsyncEnumerable<AgentExecutionEvent> ExecuteStreamAsync(
        string agentId,
        string input,
        AgentToolInvoker? toolInvoker = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var agent = FindAgent(agentId);

        if (agent is null)
        {
            yield return AgentExecutionEvent.Failed(
                $"Agent '{agentId}' was not found.",
                "AgentNotFound");

            yield break;
        }

        await foreach (var executionEvent in ExecuteAgentStreamAsync(
                           agent,
                           new AgentQuery(input),
                           toolInvoker ?? this.toolInvoker,
                           cancellationToken))
        {
            yield return executionEvent;
        }
    }

    /// <summary>
    /// Agent cevabini agent kimligine gÃ¶re runtime query bilgisiyle event stream olarak Ã¼retir.
    /// </summary>
    /// <param name="agentId">Ã‡alistirilacak agent kimligidir.</param>
    /// <param name="query">Agent'a gÃ¶nderilecek runtime query bilgisidir.</param>
    /// <param name="toolInvoker">Varsa bu Ã§agri iÃ§in kullanilacak tool invoker Ã¶rnegidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alismasi sirasinda Ã¼retilen olay stream'idir.</returns>
    public async IAsyncEnumerable<AgentExecutionEvent> ExecuteStreamAsync(
        string agentId,
        AgentQuery query,
        AgentToolInvoker? toolInvoker = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var agent = FindAgent(agentId);

        if (agent is null)
        {
            yield return AgentExecutionEvent.Failed(
                $"Agent '{agentId}' was not found.",
                "AgentNotFound");

            yield break;
        }

        await foreach (var executionEvent in ExecuteAgentStreamAsync(
                           agent,
                           query,
                           toolInvoker ?? this.toolInvoker,
                           cancellationToken))
        {
            yield return executionEvent;
        }
    }

    /// <summary>
    /// Agent cevabini tek seferlik sonuÃ§ olarak Ã¼retir.
    /// </summary>
    /// <param name="agent">Ã‡alistirilacak agent tanimidir.</param>
    /// <param name="query">Agent'a gÃ¶nderilecek runtime query bilgisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alistirma sonucudur.</returns>
    private async Task<AgentExecutionResult> ExecuteAgentAsync(
        Agent agent,
        AgentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Message))
        {
            return AgentExecutionResult.Failure(
                errorCode: "InputRequired",
                errorMessage: "Agent input cannot be empty.");
        }

        var resultBuilder = new AgentExecutionResultBuilder();

        await foreach (var executionEvent in ExecuteAgentStreamAsync(
                           agent,
                           query,
                           toolInvoker,
                           cancellationToken))
        {
            resultBuilder.Apply(executionEvent);
        }

        var result = resultBuilder.Build();

        return result.IsSuccess && string.IsNullOrWhiteSpace(result.Message)
            ? AgentExecutionResult.Failure(
                errorCode: "AgentExecutionEmptyMessage",
                errorMessage: "Agent execution completed without producing a message.",
                steps: result.Steps,
                rag: result.Rag)
            : result;
    }

    /// <summary>
    /// Agent cevabini event stream olarak Ã¼retir.
    /// </summary>
    /// <param name="agent">Ã‡alistirilacak agent tanimidir.</param>
    /// <param name="query">Agent'a gÃ¶nderilecek runtime query bilgisidir.</param>
    /// <param name="toolInvoker">Tool Ã§agrilarini Ã§alistiracak invoker Ã¶rnegidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent Ã§alismasi sirasinda Ã¼retilen olay stream'idir.</returns>
    private async IAsyncEnumerable<AgentExecutionEvent> ExecuteAgentStreamAsync(
        Agent agent,
        AgentQuery query,
        AgentToolInvoker? toolInvoker = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Message))
        {
            yield return AgentExecutionEvent.Failed(
                "Agent input cannot be empty.",
                "InputRequired");

            yield break;
        }

        var runtimeContext = new AgentRuntimeContext();

        AgentExecutionEvent? ragConfigurationFailure = null;
        if (agent.Rag is { Enabled: true } ragOptions)
        {
            try
            {
                AgentRagPolicyValidator.Validate(ragOptions, requireIndex: false);
            }
            catch (ArgumentException exception)
            {
                ragConfigurationFailure = AgentExecutionEvent.Failed(
                    $"RAG configuration is invalid for agent '{agent.Id}'; the model was not invoked. {exception.Message}",
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
            yield return AgentExecutionEvent.FromRagSearch(new RagSearchStarted(
                correlationId, agent.Id, query.ConversationId, indexName, query.Message, effectiveQuery: null,
                activeRag.Acceptance.CandidateCount));

            if (ragRetriever is null)
            {
                yield return AgentExecutionEvent.FromRagSearch(new RagSearchFailed(
                    correlationId, agent.Id, query.ConversationId, indexName, query.Message, effectiveQuery: null,
                    activeRag.Acceptance.CandidateCount, RetrievalErrorCode.InvalidRequest, TimeSpan.Zero));
                yield return AgentExecutionEvent.Failed(
                    $"RAG is enabled for agent '{agent.Id}', but the retrieval service is unavailable; the model was not invoked.",
                    "RagConfigurationInvalid",
                    CreateRagMetadata(activeRag, runtimeContext, modelInvocationSkipped: true, noContextBehaviorApplied: false));
                yield break;
            }

            var retrievalStopwatch = Stopwatch.StartNew();
            Exception? retrievalFailure = null;
            try
            {
                runtimeContext = await SearchRagContextAsync(
                    activeRag, indexName, query.Message, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                retrievalFailure = exception;
            }

            retrievalStopwatch.Stop();
            if (retrievalFailure is not null)
            {
                yield return AgentExecutionEvent.FromRagSearch(new RagSearchFailed(
                    correlationId, agent.Id, query.ConversationId, indexName, query.Message, effectiveQuery: null,
                    activeRag.Acceptance.CandidateCount, ClassifyRetrievalFailure(retrievalFailure),
                    retrievalStopwatch.Elapsed));
                yield return AgentExecutionEvent.Failed(
                    $"RAG retrieval failed for agent '{agent.Id}'; the model was not invoked.",
                    "RagRetrievalFailed",
                    CreateRagMetadata(activeRag, runtimeContext, modelInvocationSkipped: true, noContextBehaviorApplied: false));
                yield break;
            }

            yield return AgentExecutionEvent.FromRagSearch(CreateRagSearchCompleted(
                correlationId, query.ConversationId, agent, activeRag, indexName, query.Message, runtimeContext,
                retrievalStopwatch.Elapsed));
        }

        if (agent.Rag is { Enabled: true } policy && !runtimeContext.HasContext)
        {
            switch (policy.NoContextBehavior)
            {
                case RagNoContextBehavior.ReturnNotFound:
                    yield return AgentExecutionEvent.AssistantDelta(NoContextMessage);
                    yield return AgentExecutionEvent.Completed(
                        CreateRagMetadata(policy, runtimeContext, modelInvocationSkipped: true, noContextBehaviorApplied: true));
                    yield break;

                case RagNoContextBehavior.FailExecution:
                    yield return AgentExecutionEvent.Failed(
                        NoContextMessage,
                        "RagContextUnavailable",
                        CreateRagMetadata(policy, runtimeContext, modelInvocationSkipped: true, noContextBehaviorApplied: true));
                    yield break;

                case RagNoContextBehavior.AnswerNormally:
                    break;

                default:
                    yield return AgentExecutionEvent.Failed(
                        $"RAG configuration is invalid for agent '{agent.Id}'; the model was not invoked.",
                        "RagConfigurationInvalid");
                    yield break;
            }
        }

        var validationFailure = ValidateProviderRuntime(agent);

        if (validationFailure is not null)
        {
            yield return AgentExecutionEvent.Failed(
                validationFailure.ErrorMessage ?? "Agent stream request failed.",
                validationFailure.ErrorCode,
                CreateRagMetadata(
                    agent.Rag,
                    runtimeContext,
                    modelInvocationSkipped: true,
                    noContextBehaviorApplied: agent.Rag?.Enabled == true && !runtimeContext.HasContext));

            yield break;
        }

        // Resolve the configured named-model override once so every request, including tool continuations,
        // uses the same Core model identity before capability validation occurs in the chat client boundary.
        var effectiveModel = ProviderModelReferenceResolver.Resolve(agent.ModelReference, agent.Provider);

        var endpoint = ProviderDefaults.ResolveUrl(
            agent.ProviderName,
            agent.Id,
            agent.Provider?.Url);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, agent.Instructions),
        };

        if (agent.Rag is { Enabled: true } ragPolicy)
        {
            messages.Add(new ChatMessage(
                ChatRole.System,
                AgentInstructionsBuilder.BuildPolicy(ragPolicy.Mode, runtimeContext.HasContext)));

            var externalContext = AgentInstructionsBuilder.BuildExternalContext(runtimeContext);
            if (externalContext is not null)
            {
                messages.Add(new ChatMessage(ChatRole.User, externalContext));
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, query.Message));
        string? previousResponseId = null;

        while (true)
        {
            var options = new ChatRequestOptions
            {
                ReasoningEffort = agent.ReasoningEffort,
                Verbosity = agent.Verbosity
            };
            if (!string.IsNullOrWhiteSpace(previousResponseId))
            {
                options.Extensions["previous_response_id"] = previousResponseId;
            }

            var chatRequest = new ChatRequest(
                effectiveModel,
                messages,
                endpoint,
                agent.ApiKey,
                agent.Tools.Select(MapToolDefinition).ToArray(),
                Options: options);
            var client = chatClientResolver.Resolve(chatRequest);
            var toolCalls = new List<ChatToolCall>();

            await foreach (var update in client.CompleteStreamingAsync(chatRequest, cancellationToken))
            {
                previousResponseId = update.ProviderResponseId ?? previousResponseId;
                if (update.Kind == ChatStreamingUpdateKind.ContentDelta && !string.IsNullOrEmpty(update.ContentDelta))
                {
                    yield return AgentExecutionEvent.AssistantDelta(update.ContentDelta);
                }
                else if (update.Kind == ChatStreamingUpdateKind.ToolCallDelta && update.ToolCall is not null)
                {
                    toolCalls.Add(update.ToolCall);
                }
            }

            if (toolCalls.Count == 0)
            {
                yield return AgentExecutionEvent.Completed(
                    CreateRagMetadata(
                        agent.Rag,
                        runtimeContext,
                        modelInvocationSkipped: false,
                        noContextBehaviorApplied: agent.Rag?.Enabled == true && !runtimeContext.HasContext));
                yield break;
            }

            messages.Add(new ChatMessage(ChatRole.Assistant, string.Empty, ToolCalls: toolCalls));
            foreach (var toolCall in toolCalls)
            {
                yield return AgentExecutionEvent.ToolCallStarted(toolCall.Id, toolCall.Name, toolCall.ArgumentsJson);
                var result = await toolInvoker!.InvokeAsync(agent, toolCall.Name, toolCall.ArgumentsJson, cancellationToken);
                string output;
                if (result.IsSuccess)
                {
                    output = string.IsNullOrWhiteSpace(result.OutputJson) ? "{}" : result.OutputJson;
                    yield return AgentExecutionEvent.ToolCallCompleted(toolCall.Id, toolCall.Name, output);
                }
                else
                {
                    output = JsonSerializer.Serialize(new
                    {
                        isSuccess = false,
                        errorCode = result.ErrorCode ?? "ToolExecutionFailed",
                        errorMessage = result.ErrorMessage ?? "Tool execution failed."
                    });
                    yield return AgentExecutionEvent.ToolCallFailed(
                        toolCall.Id,
                        toolCall.Name,
                        result.ErrorMessage ?? "Tool execution failed.",
                        result.ErrorCode);
                }
                messages.Add(new ChatMessage(ChatRole.Tool, output, toolCall.Id));
            }
        }
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
    /// Kayitli agent koleksiyonu iÃ§inde agent kimligine gÃ¶re arama yapar.
    /// </summary>
    /// <param name="agentId">Aranacak agent kimligidir.</param>
    /// <returns>Bulunan agent tanimidir; bulunamazsa null dÃ¶ner.</returns>
    private Agent? FindAgent(string agentId)
    {
        return agents.FirstOrDefault(agent =>
            string.Equals(agent.Id, agentId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves manually supplied Core clients while preserving the runtime's provider-neutral boundary.
    /// </summary>
    private sealed class FixedChatClientResolver : IChatClientResolver
    {
        private readonly IChatClient responsesClient;
        private readonly IChatClient compatibleClient;

        /// <summary>
        /// Initializes a resolver for the two supported chat protocol families.
        /// </summary>
        public FixedChatClientResolver(IChatClient responsesClient, IChatClient compatibleClient)
        {
            this.responsesClient = responsesClient ?? throw new ArgumentNullException(nameof(responsesClient));
            this.compatibleClient = compatibleClient ?? throw new ArgumentNullException(nameof(compatibleClient));
        }

        /// <inheritdoc />
        public IChatClient Resolve(ChatRequest request) =>
            string.Equals(request.Model.ProviderName, "openai", StringComparison.OrdinalIgnoreCase)
                ? responsesClient
                : compatibleClient;
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
        var candidates = await ragRetriever!.RetrieveAsync(
            new RagQuery
            {
                Text = query,
                IndexName = indexName,
                TopK = ragOptions.Acceptance.CandidateCount,
            },
            cancellationToken).ConfigureAwait(false);

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
            noContextReason);
    }

    private static RetrievalErrorCode ClassifyRetrievalFailure(Exception exception) =>
        exception is RagRetrievalExecutionException retrievalException
            ? retrievalException.ErrorCode
            : RetrievalErrorCode.RetrievalFailed;

    private static RagSearchCompleted CreateRagSearchCompleted(
        string correlationId,
        string conversationId,
        Agent agent,
        AgentRagOptions options,
        string indexName,
        string originalQuery,
        AgentRuntimeContext runtimeContext,
        TimeSpan duration)
    {
        var topCandidate = runtimeContext.RetrievedRagCandidates.FirstOrDefault(candidate =>
            candidate is not null &&
            double.IsFinite(candidate.RawScore) &&
            !string.IsNullOrWhiteSpace(candidate.Metric) &&
            (candidate.Relevance is null ||
                double.IsFinite(candidate.Relevance.Value) && candidate.Relevance.Value is >= 0 and <= 1));

        return new RagSearchCompleted(
            correlationId,
            agent.Id,
            conversationId,
            indexName,
            originalQuery,
            effectiveQuery: null,
            options.Acceptance.CandidateCount,
            runtimeContext.RetrievedRagCandidates.Count,
            runtimeContext.RetrievedRagContext.Count,
            runtimeContext.RejectedRagCandidates.Count,
            runtimeContext.RetrievedRagContext
                .Select(result => new RagSearchSelectedResult(result.Chunk.DocumentId, result.Chunk.Id,
                    result.RawScore, result.Relevance, result.Metric, result.HigherIsBetter))
                .ToArray(),
            runtimeContext.RejectedRagCandidates
                .Select(result => new RagSearchRejectedResult(
                    result.Result.Chunk.DocumentId,
                    result.Result.Chunk.Id,
                    result.Result.RawScore,
                    result.Result.Relevance,
                    result.Reason))
                .ToArray(),
            options.Acceptance.MaximumAcceptedResults,
            duration,
            topCandidate?.RawScore,
            topCandidate?.Relevance,
            runtimeContext.NoContextReason);
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
            runtimeContext.HasContext,
            noContextBehaviorApplied ? options.NoContextBehavior : null,
            runtimeContext.NoContextReason,
            modelInvocationSkipped,
            !modelInvocationSkipped &&
                runtimeContext.HasContext &&
                options.Mode is RagExecutionMode.Grounded or RagExecutionMode.Required,
            runtimeContext.RetrievedRagCandidates,
            runtimeContext.RetrievedRagContext,
            runtimeContext.RejectedRagCandidates);
    }

}
