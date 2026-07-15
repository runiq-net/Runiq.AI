using System.Runtime.CompilerServices;
using Runiq.AI.Agents.Providers;
using Runiq.AI.Agents.Providers.OpenAI;
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
    private readonly OpenAIResponsesClient openAIResponsesClient;
    private readonly OpenAICompatibleClient openAICompatibleClient;
    private readonly AgentToolInvoker toolInvoker;
    private readonly IReadOnlyList<ContextSpace> contextSpaces;
    private readonly IContextSpaceSkillDiscoveryService skillDiscoveryService;
    private readonly IContextSpaceSourceSearchService sourceSearchService;
    private readonly IRagRetriever? ragRetriever;
    private readonly IVectorQueryTool? vectorQueryTool;


    /// <summary>
    /// Yeni bir agent execution runtime örnegi olusturur.
    /// </summary>
    /// <param name="agents">Runtime tarafindan çalistirilabilecek kayitli agent koleksiyonudur.</param>
    /// <param name="openAIResponsesClient">OpenAI Responses API provider çagrilarini yürüten client örnegidir.</param>
    /// <param name="openAICompatibleClient">OpenAI-compatible provider çagrilarini yürüten client örnegidir.</param>
    /// <param name="toolInvoker">Agent tool çagrilarini çalistiran invoker örnegidir.</param>
    /// <param name="contextSpaces">Agent çalismasinda kullanilabilecek context space tanimlaridir.</param>
    /// <param name="skillDiscoveryService">Context space skill kesif servisidir.</param>
    /// <param name="sourceSearchService">Context source arama servisidir.</param>
    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        OpenAIResponsesClient openAIResponsesClient,
        OpenAICompatibleClient openAICompatibleClient,
        AgentToolInvoker toolInvoker,
        IReadOnlyList<ContextSpace>? contextSpaces = null,
        IContextSpaceSkillDiscoveryService? skillDiscoveryService = null,
        IContextSpaceSourceSearchService? sourceSearchService = null)
        : this(
            agents,
            openAIResponsesClient,
            openAICompatibleClient,
            toolInvoker,
            contextSpaces,
            skillDiscoveryService,
            sourceSearchService,
            ragRetriever: null)
    {
    }

    /// <summary>
    /// Yeni bir agent execution runtime örnegi ve RAG retriever entegrasyonu olusturur.
    /// </summary>
    /// <param name="agents">Runtime tarafindan çalistirilabilecek kayitli agent koleksiyonudur.</param>
    /// <param name="openAIResponsesClient">OpenAI Responses API provider çagrilarini yürüten client örnegidir.</param>
    /// <param name="openAICompatibleClient">OpenAI-compatible provider çagrilarini yürüten client örnegidir.</param>
    /// <param name="toolInvoker">Agent tool çagrilarini çalistiran invoker örnegidir.</param>
    /// <param name="ragRetriever">Agent RAG sorgularini çalistiracak opsiyonel retriever servisidir.</param>
    /// <param name="contextSpaces">Agent çalismasinda kullanilabilecek context space tanimlaridir.</param>
    /// <param name="skillDiscoveryService">Context space skill kesif servisidir.</param>
    /// <param name="sourceSearchService">Context source arama servisidir.</param>
    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        OpenAIResponsesClient openAIResponsesClient,
        OpenAICompatibleClient openAICompatibleClient,
        AgentToolInvoker toolInvoker,
        IRagRetriever? ragRetriever,
        IReadOnlyList<ContextSpace>? contextSpaces = null,
        IContextSpaceSkillDiscoveryService? skillDiscoveryService = null,
        IContextSpaceSourceSearchService? sourceSearchService = null)
        : this(
            agents,
            openAIResponsesClient,
            openAICompatibleClient,
            toolInvoker,
            contextSpaces,
            skillDiscoveryService,
            sourceSearchService,
            ragRetriever)
    {
    }

    /// <summary>
    /// Yeni bir agent execution runtime örnegi olusturur.
    /// </summary>
    /// <param name="agents">Runtime tarafindan çalistirilabilecek kayitli agent koleksiyonudur.</param>
    /// <param name="openAIResponsesClient">OpenAI Responses API provider çagrilarini yürüten client örnegidir.</param>
    /// <param name="openAICompatibleClient">OpenAI-compatible provider çagrilarini yürüten client örnegidir.</param>
    /// <param name="toolInvoker">Agent tool çagrilarini çalistiran invoker örnegidir.</param>
    /// <param name="contextSpaces">Agent çalismasinda kullanilabilecek context space tanimlaridir.</param>
    /// <param name="skillDiscoveryService">Context space skill kesif servisidir.</param>
    /// <param name="sourceSearchService">Context source arama servisidir.</param>
    /// <param name="ragRetriever">Agent RAG sorgularini çalistiracak opsiyonel retriever servisidir.</param>
    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        OpenAIResponsesClient openAIResponsesClient,
        OpenAICompatibleClient openAICompatibleClient,
        AgentToolInvoker toolInvoker,
        IReadOnlyList<ContextSpace>? contextSpaces,
        IContextSpaceSkillDiscoveryService? skillDiscoveryService,
        IContextSpaceSourceSearchService? sourceSearchService,
        IRagRetriever? ragRetriever)
        : this(
            agents,
            openAIResponsesClient,
            openAICompatibleClient,
            toolInvoker,
            contextSpaces,
            skillDiscoveryService,
            sourceSearchService,
            ragRetriever,
            vectorQueryTool: null)
    {
    }

    /// <summary>
    /// Yeni bir agent execution runtime örnegi ile RAG retriever ve Vector Query Tool entegrasyonu olusturur.
    /// </summary>
    /// <param name="agents">Runtime tarafindan çalistirilabilecek kayitli agent koleksiyonudur.</param>
    /// <param name="openAIResponsesClient">OpenAI Responses API provider çagrilarini yürüten client örnegidir.</param>
    /// <param name="openAICompatibleClient">OpenAI-compatible provider çagrilarini yürüten client örnegidir.</param>
    /// <param name="toolInvoker">Agent tool çagrilarini çalistiran invoker örnegidir.</param>
    /// <param name="ragRetriever">Agent RAG sorgularini çalistiracak opsiyonel retriever servisidir.</param>
    /// <param name="vectorQueryTool">Agent'a bagli Vector Query Tool sorgularini çalistiracak opsiyonel tool örnegidir.</param>
    /// <param name="contextSpaces">Agent çalismasinda kullanilabilecek context space tanimlaridir.</param>
    /// <param name="skillDiscoveryService">Context space skill kesif servisidir.</param>
    /// <param name="sourceSearchService">Context source arama servisidir.</param>
    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        OpenAIResponsesClient openAIResponsesClient,
        OpenAICompatibleClient openAICompatibleClient,
        AgentToolInvoker toolInvoker,
        IRagRetriever? ragRetriever,
        IVectorQueryTool? vectorQueryTool,
        IReadOnlyList<ContextSpace>? contextSpaces = null,
        IContextSpaceSkillDiscoveryService? skillDiscoveryService = null,
        IContextSpaceSourceSearchService? sourceSearchService = null)
        : this(
            agents,
            openAIResponsesClient,
            openAICompatibleClient,
            toolInvoker,
            contextSpaces,
            skillDiscoveryService,
            sourceSearchService,
            ragRetriever,
            vectorQueryTool)
    {
    }

    /// <summary>
    /// Tüm bagimliliklari atayan çekirdek runtime kurucu metodudur.
    /// </summary>
    /// <param name="agents">Runtime tarafindan çalistirilabilecek kayitli agent koleksiyonudur.</param>
    /// <param name="openAIResponsesClient">OpenAI Responses API provider çagrilarini yürüten client örnegidir.</param>
    /// <param name="openAICompatibleClient">OpenAI-compatible provider çagrilarini yürüten client örnegidir.</param>
    /// <param name="toolInvoker">Agent tool çagrilarini çalistiran invoker örnegidir.</param>
    /// <param name="contextSpaces">Agent çalismasinda kullanilabilecek context space tanimlaridir.</param>
    /// <param name="skillDiscoveryService">Context space skill kesif servisidir.</param>
    /// <param name="sourceSearchService">Context source arama servisidir.</param>
    /// <param name="ragRetriever">Agent RAG sorgularini çalistiracak opsiyonel retriever servisidir.</param>
    /// <param name="vectorQueryTool">Agent'a bagli Vector Query Tool sorgularini çalistiracak opsiyonel tool örnegidir.</param>
    private AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        OpenAIResponsesClient openAIResponsesClient,
        OpenAICompatibleClient openAICompatibleClient,
        AgentToolInvoker toolInvoker,
        IReadOnlyList<ContextSpace>? contextSpaces,
        IContextSpaceSkillDiscoveryService? skillDiscoveryService,
        IContextSpaceSourceSearchService? sourceSearchService,
        IRagRetriever? ragRetriever,
        IVectorQueryTool? vectorQueryTool)
    {
        this.agents = agents;
        this.openAIResponsesClient = openAIResponsesClient;
        this.openAICompatibleClient = openAICompatibleClient;
        this.toolInvoker = toolInvoker;
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

        var endpoint = ProviderDefaults.ResolveUrl(agent);
        var providerDefault = ProviderDefaults.Get(agent.ProviderName);

        switch (providerDefault.Protocol)
        {
            case ProviderProtocol.OpenAICompatible:
                await foreach (var executionEvent in ExecuteOpenAICompatibleStreamAsync(
                          agent,
                          endpoint,
                          query.Message,
                          instructions,
                          toolInvoker,
                          cancellationToken))
                {
                    yield return executionEvent;
                }

                yield break;

            case ProviderProtocol.Ollama:
                var ollamaResult = await ExecuteOllamaAsync(
                    agent,
                    endpoint,
                    query.Message,
                    cancellationToken);

                if (!ollamaResult.IsSuccess)
                {
                    yield return AgentExecutionEvent.Failed(
                        ollamaResult.ErrorMessage ?? "Ollama execution failed.",
                        ollamaResult.ErrorCode);

                    yield break;
                }

                if (!string.IsNullOrWhiteSpace(ollamaResult.Message))
                {
                    yield return AgentExecutionEvent.AssistantDelta(ollamaResult.Message);
                }

                yield return AgentExecutionEvent.Completed();
                yield break;

            default:
                yield return AgentExecutionEvent.Failed(
                    $"Provider protocol '{providerDefault.Protocol}' is not supported.",
                    "UnsupportedProviderProtocol");

                yield break;
        }
    }

    /// <summary>
    /// OpenAI veya OpenAI-compatible provider için stream çalistirmasini yürütür.
    /// </summary>
    /// <param name="agent">Çalistirilacak agent tanimidir.</param>
    /// <param name="endpoint">Provider endpoint adresidir.</param>
    /// <param name="input">Agent'a gönderilecek kullanici girdisidir.</param>
    /// <param name="instructions">Runtime context ile zenginlestirilmis system yönergeleridir.</param>
    /// <param name="toolInvoker">Tool çagrilarini çalistiracak invoker örnegidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Provider tarafindan üretilen agent execution event stream'idir.</returns>
    private async IAsyncEnumerable<AgentExecutionEvent> ExecuteOpenAICompatibleStreamAsync(
        Agent agent,
        Uri endpoint,
        string input,
        string instructions,
        AgentToolInvoker? toolInvoker,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (IsNativeOpenAIProvider(agent))
        {
            await foreach (var executionEvent in openAIResponsesClient.StreamAsync(
                               agent: agent,
                               endpoint: endpoint,
                               input: input,
                               toolInvoker: toolInvoker,
                               instructions: instructions,
                               cancellationToken: cancellationToken))
            {
                yield return executionEvent;
            }
            yield break;
        }
        await foreach (var executionEvent in openAICompatibleClient.StreamAsync(
                           agent: agent,
                           endpoint: endpoint,
                           input: input,
                           instructions: instructions,
                           cancellationToken: cancellationToken))
        {
            yield return executionEvent;
        }
    }

    /// <summary>
    /// Ollama provider için geçici agent çalistirma sonucunu üretir.
    /// </summary>
    /// <param name="agent">Çalistirilacak agent tanimidir.</param>
    /// <param name="endpoint">Ollama endpoint adresidir.</param>
    /// <param name="input">Agent'a gönderilecek kullanici girdisidir.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Agent çalistirma sonucudur.</returns>
    private static Task<AgentExecutionResult> ExecuteOllamaAsync(
        Agent agent,
        Uri endpoint,
        string input,
        CancellationToken cancellationToken)
    {
        var message =
            $"Agent '{agent.Name}' will call Ollama endpoint '{endpoint}' with model '{agent.ModelName}'. Input: {input}";

        return Task.FromResult(AgentExecutionResult.Success(message));
    }

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
    /// Agent'in native OpenAI provider kullanip kullanmadigini belirtir.
    /// </summary>
    /// <param name="agent">Kontrol edilecek agent tanimidir.</param>
    /// <returns>Agent native OpenAI provider kullaniyorsa true döner.</returns>
    private static bool IsNativeOpenAIProvider(Agent agent)
    {
        return string.Equals(
            agent.ProviderName,
            "openai",
            StringComparison.OrdinalIgnoreCase);
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

