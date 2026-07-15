using System.Runtime.CompilerServices;
using System.Text.Json;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Metadata;
using Runiq.AI.Core.Providers;
using Runiq.AI.Agents.Tools;
using Runiq.AI.ContextSpaces.Models.Sources;
using Runiq.AI.ContextSpaces.Services;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Tools;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.Tools;

namespace Runiq.AI.Agents.Runtime;

/// <summary>
/// Kayitli agent tanimlarini provider pipeline'i üzerinden çalistiran runtime servisidir.
/// </summary>
public sealed class AgentExecutionRuntime
{
    private const double MinSelectedSourceScore = 8.0;
    private const double RelativeTopSourceScoreRatio = 0.45;
    private const int MaxSelectedSourceExcerptCount = 4;

    private readonly IEnumerable<Agent> agents;
    private readonly IChatClientResolver chatClientResolver;
    private readonly AgentToolInvoker toolInvoker;
    private readonly IReadOnlyList<ContextSpace> contextSpaces;
    private readonly IContextSpaceSkillDiscoveryService skillDiscoveryService;
    private readonly IContextSpaceSourceSearchService sourceSearchService;
    private readonly IRagRetriever? ragRetriever;
    private readonly IVectorQueryTool? vectorQueryTool;

    /// <summary>
    /// Initializes the runtime with two provider-neutral clients for compatibility with existing manual construction.
    /// </summary>
    /// <param name="agents">The registered agents available to the runtime.</param>
    /// <param name="openAIResponsesClient">The client used for native OpenAI requests.</param>
    /// <param name="openAICompatibleClient">The client used for OpenAI-compatible and Ollama requests.</param>
    /// <param name="toolInvoker">The agent-owned tool invoker.</param>
    /// <param name="contextSpaces">Optional context spaces.</param>
    /// <param name="skillDiscoveryService">Optional skill discovery service.</param>
    /// <param name="sourceSearchService">Optional context source search service.</param>
    /// <param name="ragRetriever">Optional RAG retriever.</param>
    /// <param name="vectorQueryTool">Optional vector query tool.</param>
    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        IChatClient openAIResponsesClient,
        IChatClient openAICompatibleClient,
        AgentToolInvoker toolInvoker,
        IReadOnlyList<ContextSpace>? contextSpaces = null,
        IContextSpaceSkillDiscoveryService? skillDiscoveryService = null,
        IContextSpaceSourceSearchService? sourceSearchService = null,
        IRagRetriever? ragRetriever = null,
        IVectorQueryTool? vectorQueryTool = null)
        : this(
            agents,
            new FixedChatClientResolver(openAIResponsesClient, openAICompatibleClient),
            toolInvoker,
            contextSpaces,
            skillDiscoveryService,
            sourceSearchService,
            ragRetriever,
            vectorQueryTool)
    {
    }


    /// <summary>
    /// Initializes the agent runtime with provider-neutral model resolution and agent-owned orchestration services.
    /// </summary>
    /// <param name="agents">Runtime tarafindan çalistirilabilecek kayitli agent koleksiyonudur.</param>
    /// <param name="chatClientResolver">Resolves the shared chat client for each agent model.</param>
    /// <param name="toolInvoker">Agent tool çagrilarini çalistiran invoker örnegidir.</param>
    /// <param name="contextSpaces">Agent çalismasinda kullanilabilecek context space tanimlaridir.</param>
    /// <param name="skillDiscoveryService">Context space skill kesif servisidir.</param>
    /// <param name="sourceSearchService">Context source arama servisidir.</param>
    /// <param name="ragRetriever">Agent RAG sorgularini çalistiracak opsiyonel retriever servisidir.</param>
    /// <param name="vectorQueryTool">Agent'a bagli Vector Query Tool sorgularini çalistiracak opsiyonel tool örnegidir.</param>
    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        IChatClientResolver chatClientResolver,
        AgentToolInvoker toolInvoker,
        IReadOnlyList<ContextSpace>? contextSpaces = null,
        IContextSpaceSkillDiscoveryService? skillDiscoveryService = null,
        IContextSpaceSourceSearchService? sourceSearchService = null,
        IRagRetriever? ragRetriever = null,
        IVectorQueryTool? vectorQueryTool = null)
    {
        this.agents = agents ?? throw new ArgumentNullException(nameof(agents));
        this.chatClientResolver = chatClientResolver ?? throw new ArgumentNullException(nameof(chatClientResolver));
        this.toolInvoker = toolInvoker ?? throw new ArgumentNullException(nameof(toolInvoker));
        this.contextSpaces = contextSpaces ?? [];
        this.skillDiscoveryService = skillDiscoveryService ?? new ContextSpaceSkillDiscoveryService();
        this.sourceSearchService = sourceSearchService
                ?? new ContextSpaceSourceSearchService(new ContextSpaceFileSystemSourceReader());
        this.ragRetriever = ragRetriever;
        this.vectorQueryTool = vectorQueryTool;
    }

    /// <summary>
    /// Agent cevabini agent kimligine göre tek seferlik sonuç olarak üretir.
    /// </summary>
    /// <param name="agentId">Çalistirilacak agent kimligidir.</param>
    /// <param name="input">Agent'a gönderilecek kullanici girdisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent çalistirma sonucudur.</returns>
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
    /// Agent cevabini agent kimligine göre runtime query bilgisiyle tek seferlik sonuç olarak üretir.
    /// </summary>
    /// <param name="agentId">Çalistirilacak agent kimligidir.</param>
    /// <param name="query">Agent'a gönderilecek runtime query bilgisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent çalistirma sonucudur.</returns>
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
    /// Kayit listesine bagli olmayan geçici bir agent tanimiyla tek seferlik sonuç üretir.
    /// </summary>
    /// <param name="agent">Çalistirilacak agent tanimidir.</param>
    /// <param name="input">Agent'a gönderilecek kullanici girdisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent çalistirma sonucudur.</returns>
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
    /// Kayit listesine bagli olmayan geçici bir agent tanimiyla runtime query bilgisiyle tek seferlik sonuç üretir.
    /// </summary>
    /// <param name="agent">Çalistirilacak agent tanimidir.</param>
    /// <param name="query">Agent'a gönderilecek runtime query bilgisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent çalistirma sonucudur.</returns>
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
    /// Agent cevabini agent kimligine göre event stream olarak üretir.
    /// </summary>
    /// <param name="agentId">Çalistirilacak agent kimligidir.</param>
    /// <param name="input">Agent'a gönderilecek kullanici girdisidir.</param>
    /// <param name="toolInvoker">Varsa bu çagri için kullanilacak tool invoker örnegidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent çalismasi sirasinda üretilen olay stream'idir.</returns>
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
    /// Agent cevabini agent kimligine göre runtime query bilgisiyle event stream olarak üretir.
    /// </summary>
    /// <param name="agentId">Çalistirilacak agent kimligidir.</param>
    /// <param name="query">Agent'a gönderilecek runtime query bilgisidir.</param>
    /// <param name="toolInvoker">Varsa bu çagri için kullanilacak tool invoker örnegidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent çalismasi sirasinda üretilen olay stream'idir.</returns>
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
    /// Agent cevabini tek seferlik sonuç olarak üretir.
    /// </summary>
    /// <param name="agent">Çalistirilacak agent tanimidir.</param>
    /// <param name="query">Agent'a gönderilecek runtime query bilgisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent çalistirma sonucudur.</returns>
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
                steps: result.Steps)
            : result;
    }

    /// <summary>
    /// Agent cevabini event stream olarak üretir.
    /// </summary>
    /// <param name="agent">Çalistirilacak agent tanimidir.</param>
    /// <param name="query">Agent'a gönderilecek runtime query bilgisidir.</param>
    /// <param name="toolInvoker">Tool çagrilarini çalistiracak invoker örnegidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent çalismasi sirasinda üretilen olay stream'idir.</returns>
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

        var runtimeContext = CreateRuntimeContext(agent);

        var skillLoadedEvent = CreateSkillLoadedEvent(runtimeContext);

        if (skillLoadedEvent is not null)
        {
            yield return skillLoadedEvent;
        }

        runtimeContext = await SearchRuntimeContextSourcesAsync(
            runtimeContext,
            query.Message,
            cancellationToken);

        AgentExecutionEvent? ragFailureEvent = null;

        try
        {
            runtimeContext = await SearchRagContextAsync(
                agent,
                runtimeContext,
                query,
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            ragFailureEvent = AgentExecutionEvent.Failed(
                exception.Message,
                "RagRetrievalFailed");
        }

        if (ragFailureEvent is not null)
        {
            yield return ragFailureEvent;
            yield break;
        }

        var instructions = AgentInstructionsBuilder.Build(agent, runtimeContext);

        var contextProvidedEvent = CreateContextProvidedEvent(runtimeContext);

        var contextSearchedEvent = CreateContextSearchedEvent(runtimeContext);

        if (contextSearchedEvent is not null)
        {
            yield return contextSearchedEvent;
        }

        var validationFailure = ValidateProviderRuntime(agent);

        if (validationFailure is not null)
        {
            yield return AgentExecutionEvent.Failed(
                validationFailure.ErrorMessage ?? "Agent stream request failed.",
                validationFailure.ErrorCode);

            yield break;
        }

        var endpoint = ProviderDefaults.ResolveUrl(
            agent.ProviderName,
            agent.Id,
            agent.Provider?.Url);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, instructions),
            new(ChatRole.User, query.Message)
        };
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
                agent.ModelReference,
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
                yield return AgentExecutionEvent.Completed();
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
    /// Provider runtime ayarlarinin çalistirma öncesi geçerli olup olmadigini dogrular.
    /// </summary>
    /// <param name="agent">Dogrulanacak agent tanimidir.</param>
    /// <returns>Geçersiz ayar varsa hata sonucudur; aksi halde null döner.</returns>
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
    /// Kayitli agent koleksiyonu içinde agent kimligine göre arama yapar.
    /// </summary>
    /// <param name="agentId">Aranacak agent kimligidir.</param>
    /// <returns>Bulunan agent tanimidir; bulunamazsa null döner.</returns>
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
    /// Agent'a bagli context space, skill ve source search bilgilerini runtime için çözümler.
    /// </summary>
    private AgentRuntimeContext CreateRuntimeContext(Agent agent)
    {
        if (agent.ContextSpaceIds.Count == 0 || contextSpaces.Count == 0)
        {
            return new AgentRuntimeContext([], []);
        }

        var attachedContextSpaces = contextSpaces
            .Where(contextSpace => agent.ContextSpaceIds.Any(contextSpaceId =>
                string.Equals(contextSpaceId, contextSpace.Id, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (attachedContextSpaces.Length == 0)
        {
            return new AgentRuntimeContext([], []);
        }

        var skills = attachedContextSpaces
            .SelectMany(contextSpace => skillDiscoveryService.Discover(contextSpace))
            .ToArray();

        return new AgentRuntimeContext(
            ContextSpaces: attachedContextSpaces,
            Skills: skills,
            AttachedSourceCount: attachedContextSpaces.Sum(contextSpace => contextSpace.Sources.Count));
    }

    /// <summary>
    /// Runtime context içindeki bagli source'lari arar ve seçilen excerpt bilgileriyle context'i döner.
    /// </summary>
    private async Task<AgentRuntimeContext> SearchRuntimeContextSourcesAsync(
        AgentRuntimeContext runtimeContext,
        string input,
        CancellationToken cancellationToken)
    {
        if (runtimeContext.ContextSpaces.Count == 0 || runtimeContext.AttachedSourceCount == 0)
        {
            return runtimeContext;
        }

        var sourceSearchResults = new List<ContextSpaceSourceSearchResult>();
        var searchedDocumentCount = 0;

        foreach (var contextSpace in runtimeContext.ContextSpaces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await sourceSearchService.SearchWithSummaryAsync(
                contextSpace,
                input,
                maxResults: int.MaxValue,
                cancellationToken);

            searchedDocumentCount += response.SearchedDocumentCount;
            sourceSearchResults.AddRange(response.Results);
        }

        var candidates = sourceSearchResults
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var selectedResults = SelectSourceContext(candidates);

        return new AgentRuntimeContext(
            ContextSpaces: runtimeContext.ContextSpaces,
            Skills: runtimeContext.Skills,
            AttachedSourceCount: runtimeContext.AttachedSourceCount,
            SearchedDocumentCount: searchedDocumentCount,
            CandidateCount: candidates.Length,
            SourceSearchResults: selectedResults,
            RagSearchResults: runtimeContext.RetrievedRagContext);
    }

    /// <summary>
    /// Agent RAG yapilandirmasi varsa RAG retrieval çalistirir ve sonuçlari runtime context'e ekler.
    /// </summary>
    private async Task<AgentRuntimeContext> SearchRagContextAsync(
        Agent agent,
        AgentRuntimeContext runtimeContext,
        AgentQuery query,
        CancellationToken cancellationToken)
    {
        if (agent.Rag is null || !agent.Rag.Enabled)
        {
            return runtimeContext;
        }

        // Vector Query Tool bridge: when the agent is associated with a Vector Query Tool (its RAG options carry
        // a vector store name) and a tool is available, invoke the tool instead of the direct retriever path.
        // Agents without a vector store name keep the existing retriever behavior unchanged.
        if (vectorQueryTool is not null && !string.IsNullOrWhiteSpace(agent.Rag.VectorStoreName))
        {
            return await SearchRagContextWithVectorQueryToolAsync(
                agent,
                runtimeContext,
                query,
                cancellationToken).ConfigureAwait(false);
        }

        if (ragRetriever is null)
        {
            return runtimeContext;
        }

        var indexName = query.IndexName ?? agent.Rag.IndexName;
        var results = await ragRetriever.RetrieveAsync(
            new RagQuery
            {
                Text = query.Message,
                IndexName = indexName,
            },
            cancellationToken).ConfigureAwait(false);

        return new AgentRuntimeContext(
            ContextSpaces: runtimeContext.ContextSpaces,
            Skills: runtimeContext.Skills,
            AttachedSourceCount: runtimeContext.AttachedSourceCount,
            SearchedDocumentCount: runtimeContext.SearchedDocumentCount,
            CandidateCount: runtimeContext.CandidateCount,
            SourceSearchResults: runtimeContext.RetrievedSourceContext,
            RagSearchResults: results);
    }

    /// <summary>
    /// Agent'a bagli Vector Query Tool yapilandirmasindan bir tool istegi olusturur, tool'u çalistirir ve mevcut
    /// RAG context formatina eslenmis sonuçlari runtime context'e yerlestirir. Basarisiz bir tool sonucu, mevcut
    /// RAG retriever hatasiyla ayni deterministik akisa uyacak biçimde <see cref="InvalidOperationException"/>
    /// olarak yükseltilir.
    /// </summary>
    private async Task<AgentRuntimeContext> SearchRagContextWithVectorQueryToolAsync(
        Agent agent,
        AgentRuntimeContext runtimeContext,
        AgentQuery query,
        CancellationToken cancellationToken)
    {
        var ragOptions = agent.Rag!;
        var indexName = query.IndexName ?? ragOptions.IndexName;

        var request = new VectorQueryToolRequest
        {
            VectorStoreName = ragOptions.VectorStoreName!,
            IndexName = indexName ?? string.Empty,
            QueryText = query.Message,
            EmbeddingModel = ragOptions.EmbeddingModel,
        };

        var result = await vectorQueryTool!.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.Reason)
                    ? "The Vector Query Tool retrieval failed."
                    : result.Reason);
        }

        return new AgentRuntimeContext(
            ContextSpaces: runtimeContext.ContextSpaces,
            Skills: runtimeContext.Skills,
            AttachedSourceCount: runtimeContext.AttachedSourceCount,
            SearchedDocumentCount: runtimeContext.SearchedDocumentCount,
            CandidateCount: runtimeContext.CandidateCount,
            SourceSearchResults: runtimeContext.RetrievedSourceContext,
            RagSearchResults: MapVectorQueryMatches(result.Matches));
    }

    /// <summary>
    /// Vector Query Tool sonucundaki eslesmeleri, mevcut RAG context assembly'sinin tükettigi
    /// <see cref="RagSearchResult"/> formatina dönüstürür. Içerik, skor ve metadata korunur; bos kayit kimlikleri
    /// ve eksik document kimlikleri, required chunk alanlarinin ihlal edilmemesi için güvenli degerlere düser.
    /// </summary>
    private static IReadOnlyList<RagSearchResult> MapVectorQueryMatches(
        IReadOnlyList<RetrievalResultItem> matches)
    {
        if (matches.Count == 0)
        {
            return [];
        }

        var results = new List<RagSearchResult>(matches.Count);

        foreach (var match in matches)
        {
            var chunkId = string.IsNullOrWhiteSpace(match.RecordId)
                ? "vector-query-match"
                : match.RecordId;

            var documentId =
                match.Metadata.Values.TryGetValue("documentId", out var value) &&
                !string.IsNullOrWhiteSpace(value)
                    ? value
                    : chunkId;

            results.Add(new RagSearchResult
            {
                Chunk = new RagChunk
                {
                    Id = chunkId,
                    DocumentId = documentId,
                    Content = match.Content,
                    Metadata = new RagChunkMetadata
                    {
                        AdditionalMetadata = new RagMetadata(match.Metadata.Values),
                    },
                },
                Score = match.Score,
                Metadata = new RagMetadata(match.Metadata.Values),
            });
        }

        return results;
    }

    /// <summary>
    /// Ranked source candidates içinden model context'ine gönderilecek yüksek güvenli excerpt'leri seçer.
    /// </summary>
    private static IReadOnlyList<ContextSpaceSourceSearchResult> SelectSourceContext(
        IReadOnlyList<ContextSpaceSourceSearchResult> candidates)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var topScore = candidates[0].Score;
        var minimumScore = Math.Max(
            MinSelectedSourceScore,
            topScore * RelativeTopSourceScoreRatio);

        return candidates
            .Where(candidate => candidate.Score >= minimumScore)
            .Take(MaxSelectedSourceExcerptCount)
            .ToArray();
    }

    /// <summary>
    /// Runtime context bilgisinden chat ekranina gönderilecek context saglandi olayini üretir.
    /// </summary>
    private static AgentExecutionEvent? CreateContextProvidedEvent(
        AgentRuntimeContext runtimeContext)
    {
        if (!runtimeContext.HasContext)
        {
            return null;
        }

        var contextSpaces = runtimeContext.ContextSpaces
            .Select(contextSpace => new AgentExecutionContextSpaceInfo(
                Id: contextSpace.Id,
                Name: contextSpace.Name,
                Description: contextSpace.Description))
            .ToArray();

        var skills = runtimeContext.Skills
            .Select(skill => new AgentExecutionSkillInfo(
                Id: skill.Id,
                Name: skill.Name,
                Description: skill.Description,
                Version: skill.Version,
                Tags: skill.Tags,
                SourceId: skill.SourceId,
                RelativePath: skill.RelativePath))
            .ToArray();

        var sources = runtimeContext.ContextSpaces
            .SelectMany(contextSpace => contextSpace.Sources)
            .Select(source => new AgentExecutionSourceInfo(
                Id: source.Id,
                Name: source.Name,
                Kind: source.Kind.ToString(),
                Description: source.Description))
            .ToArray();

        return AgentExecutionEvent.ContextProvided(
            contextSpaces,
            skills,
            sources);
    }

    /// <summary>
    /// Runtime context içindeki yüklenen skill bilgilerinden chat ekranina gönderilecek skill loaded olayini üretir.
    /// </summary>
    private static AgentExecutionEvent? CreateSkillLoadedEvent(
        AgentRuntimeContext runtimeContext)
    {
        if (runtimeContext.Skills.Count == 0)
        {
            return null;
        }

        var loadedSkills = runtimeContext.Skills
            .Select(skill => new AgentExecutionLoadedSkillInfo(
                SkillId: skill.Id,
                SkillName: skill.Name,
                Version: skill.Version,
                Description: skill.Description))
            .ToArray();

        return AgentExecutionEvent.SkillLoaded(loadedSkills);
    }

    /// <summary>
    /// Runtime context içindeki source arama sonuçlarindan chat ekranina gönderilecek context arandi olayini üretir.
    /// </summary>
    private static AgentExecutionEvent? CreateContextSearchedEvent(
        AgentRuntimeContext runtimeContext)
    {
        if (runtimeContext.AttachedSourceCount == 0)
        {
            return null;
        }

        var sourceSearchResults = runtimeContext.RetrievedSourceContext
            .Select(result => new AgentExecutionSourceSearchResultInfo(
                SourceId: result.SourceId,
                SourceName: result.SourceName,
                RelativePath: result.RelativePath,
                FileName: result.FileName,
                Snippet: result.Snippet,
                Score: result.Score))
            .ToArray();

        var summary = new AgentExecutionContextSearchSummaryInfo(
            AttachedSourceCount: runtimeContext.AttachedSourceCount,
            SearchedDocumentCount: runtimeContext.SearchedDocumentCount,
            CandidateCount: runtimeContext.CandidateCount,
            SelectedCount: runtimeContext.SelectedCount);

        return AgentExecutionEvent.ContextSearched(summary, sourceSearchResults);
    }


}

