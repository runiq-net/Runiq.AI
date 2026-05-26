using System.Runtime.CompilerServices;
using System.Text;
using Runiq.Agents.Providers;
using Runiq.Agents.Providers.OpenAI;
using Runiq.Agents.Tools;
using Runiq.ContextSpaces.Models.Sources;
using Runiq.ContextSpaces.Services;

namespace Runiq.Agents.Runtime;

/// <summary>
/// Kayıtlı agent tanımlarını provider pipeline'ı üzerinden çalıştıran runtime servisidir.
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


    /// <summary>
    /// Yeni bir agent execution runtime örneği oluşturur.
    /// </summary>
    /// <param name="agents">Runtime tarafından çalıştırılabilecek kayıtlı agent koleksiyonudur.</param>
    /// <param name="openAIResponsesClient">OpenAI Responses API provider çağrılarını yürüten client örneğidir.</param>
    /// <param name="openAICompatibleClient">OpenAI-compatible provider çağrılarını yürüten client örneğidir.</param>
    /// <param name="toolInvoker">Agent tool çağrılarını çalıştıran invoker örneğidir.</param>
    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        OpenAIResponsesClient openAIResponsesClient,
        OpenAICompatibleClient openAICompatibleClient,
        AgentToolInvoker toolInvoker,
        IReadOnlyList<ContextSpace>? contextSpaces = null,
        IContextSpaceSkillDiscoveryService? skillDiscoveryService = null,
        IContextSpaceSourceSearchService? sourceSearchService = null)
    {
        this.agents = agents;
        this.openAIResponsesClient = openAIResponsesClient;
        this.openAICompatibleClient = openAICompatibleClient;
        this.toolInvoker = toolInvoker;
        this.contextSpaces = contextSpaces ?? [];
        this.skillDiscoveryService = skillDiscoveryService ?? new ContextSpaceSkillDiscoveryService();
        this.sourceSearchService = sourceSearchService
                ?? new ContextSpaceSourceSearchService(new ContextSpaceFileSystemSourceReader());
    }

    /// <summary>
    /// Agent cevabını agent kimliğine göre tek seferlik sonuç olarak üretir.
    /// </summary>
    /// <param name="agentId">Çalıştırılacak agent kimliğidir.</param>
    /// <param name="input">Agent'a gönderilecek kullanıcı girdisidir.</param>
    /// <param name="cancellationToken">İptal bildirimidir.</param>
    /// <returns>Agent çalıştırma sonucudur.</returns>
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
            input,
            cancellationToken);
    }

    /// <summary>
    /// Kayıt listesine bağlı olmayan geçici bir agent tanımıyla tek seferlik sonuç üretir.
    /// </summary>
    /// <param name="agent">Çalıştırılacak agent tanımıdır.</param>
    /// <param name="input">Agent'a gönderilecek kullanıcı girdisidir.</param>
    /// <param name="cancellationToken">İptal bildirimidir.</param>
    /// <returns>Agent çalıştırma sonucudur.</returns>
    public Task<AgentExecutionResult> ExecuteAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAgentAsync(
            agent,
            input,
            cancellationToken);
    }

    /// <summary>
    /// Agent cevabını agent kimliğine göre event stream olarak üretir.
    /// </summary>
    /// <param name="agentId">Çalıştırılacak agent kimliğidir.</param>
    /// <param name="input">Agent'a gönderilecek kullanıcı girdisidir.</param>
    /// <param name="toolInvoker">Varsa bu çağrı için kullanılacak tool invoker örneğidir.</param>
    /// <param name="cancellationToken">İptal bildirimidir.</param>
    /// <returns>Agent çalışması sırasında üretilen olay stream'idir.</returns>
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
                           input,
                           toolInvoker ?? this.toolInvoker,
                           cancellationToken))
        {
            yield return executionEvent;
        }
    }

    /// <summary>
    /// Agent cevabını tek seferlik sonuç olarak üretir.
    /// </summary>
    /// <param name="agent">Çalıştırılacak agent tanımıdır.</param>
    /// <param name="input">Agent'a gönderilecek kullanıcı girdisidir.</param>
    /// <param name="cancellationToken">İptal bildirimidir.</param>
    /// <returns>Agent çalıştırma sonucudur.</returns>
    private async Task<AgentExecutionResult> ExecuteAgentAsync(
        Agent agent,
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (string.IsNullOrWhiteSpace(input))
        {
            return AgentExecutionResult.Failure(
                errorCode: "InputRequired",
                errorMessage: "Agent input cannot be empty.");
        }

        var messageBuilder = new StringBuilder();

        await foreach (var executionEvent in ExecuteAgentStreamAsync(
                           agent,
                           input,
                           toolInvoker,
                           cancellationToken))
        {
            switch (executionEvent.Kind)
            {
                case AgentExecutionEventKind.AssistantDelta:
                    messageBuilder.Append(executionEvent.Content);
                    break;

                case AgentExecutionEventKind.Failed:
                    return AgentExecutionResult.Failure(
                        errorCode: executionEvent.ErrorCode ?? "AgentExecutionFailed",
                        errorMessage: executionEvent.ErrorMessage ?? "Agent execution failed.");

                case AgentExecutionEventKind.Completed:
                    var message = messageBuilder.ToString();

                    return string.IsNullOrWhiteSpace(message)
                        ? AgentExecutionResult.Failure(
                            errorCode: "AgentExecutionEmptyMessage",
                            errorMessage: "Agent execution completed without producing a message.")
                        : AgentExecutionResult.Success(message);
            }
        }

        var fallbackMessage = messageBuilder.ToString();

        return string.IsNullOrWhiteSpace(fallbackMessage)
            ? AgentExecutionResult.Failure(
                errorCode: "AgentExecutionEmptyMessage",
                errorMessage: "Agent execution completed without producing a message.")
            : AgentExecutionResult.Success(fallbackMessage);
    }

    /// <summary>
    /// Agent cevabını event stream olarak üretir.
    /// </summary>
    /// <param name="agent">Çalıştırılacak agent tanımıdır.</param>
    /// <param name="input">Agent'a gönderilecek kullanıcı girdisidir.</param>
    /// <param name="toolInvoker">Tool çağrılarını çalıştıracak invoker örneğidir.</param>
    /// <param name="cancellationToken">İptal bildirimidir.</param>
    /// <returns>Agent çalışması sırasında üretilen olay stream'idir.</returns>
    private async IAsyncEnumerable<AgentExecutionEvent> ExecuteAgentStreamAsync(
        Agent agent,
        string input,
        AgentToolInvoker? toolInvoker = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (string.IsNullOrWhiteSpace(input))
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
            input,
            cancellationToken);

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
                          input,
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
                    input,
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
    /// OpenAI veya OpenAI-compatible provider için stream çalıştırmasını yürütür.
    /// </summary>
    /// <param name="agent">Çalıştırılacak agent tanımıdır.</param>
    /// <param name="endpoint">Provider endpoint adresidir.</param>
    /// <param name="input">Agent'a gönderilecek kullanıcı girdisidir.</param>
    /// <param name="toolInvoker">Tool çağrılarını çalıştıracak invoker örneğidir.</param>
    /// <param name="cancellationToken">İptal bildirimidir.</param>
    /// <returns>Provider tarafından üretilen agent execution event stream'idir.</returns>
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
    /// Ollama provider için geçici agent çalıştırma sonucunu üretir.
    /// </summary>
    /// <param name="agent">Çalıştırılacak agent tanımıdır.</param>
    /// <param name="endpoint">Ollama endpoint adresidir.</param>
    /// <param name="input">Agent'a gönderilecek kullanıcı girdisidir.</param>
    /// <param name="cancellationToken">İptal bildirimidir.</param>
    /// <returns>Agent çalıştırma sonucudur.</returns>
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
    /// Provider runtime ayarlarının çalıştırma öncesi geçerli olup olmadığını doğrular.
    /// </summary>
    /// <param name="agent">Doğrulanacak agent tanımıdır.</param>
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
    /// Agent'ın native OpenAI provider kullanıp kullanmadığını belirtir.
    /// </summary>
    /// <param name="agent">Kontrol edilecek agent tanımıdır.</param>
    /// <returns>Agent native OpenAI provider kullanıyorsa true döner.</returns>
    private static bool IsNativeOpenAIProvider(Agent agent)
    {
        return string.Equals(
            agent.ProviderName,
            "openai",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kayıtlı agent koleksiyonu içinde agent kimliğine göre arama yapar.
    /// </summary>
    /// <param name="agentId">Aranacak agent kimliğidir.</param>
    /// <returns>Bulunan agent tanımıdır; bulunamazsa null döner.</returns>
    private Agent? FindAgent(string agentId)
    {
        return agents.FirstOrDefault(agent =>
            string.Equals(agent.Id, agentId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Agent'a bağlı context space, skill ve source search bilgilerini runtime için çözümler.
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
    /// Runtime context içindeki bağlı source'ları arar ve seçilen excerpt bilgileriyle context'i döner.
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
            SourceSearchResults: selectedResults);
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
    /// Runtime context bilgisinden chat ekranına gönderilecek context sağlandı olayını üretir.
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
    /// Runtime context içindeki yüklenen skill bilgilerinden chat ekranına gönderilecek skill loaded olayını üretir.
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
    /// Runtime context içindeki source arama sonuçlarından chat ekranına gönderilecek context arandı olayını üretir.
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
