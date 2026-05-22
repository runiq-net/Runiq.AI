using System.Runtime.CompilerServices;
using Runiq.Agents.Providers;
using Runiq.Agents.Providers.OpenAI;
using Runiq.Agents.Tools;

namespace Runiq.Agents.Runtime;

/// <summary>
/// Kayıtlı agent tanımlarını provider client'lar üzerinden çalıştıran runtime servisidir.
/// </summary>
public sealed class AgentExecutionRuntime
{
    private readonly IEnumerable<Agent> agents;
    private readonly OpenAIResponsesClient openAIResponsesClient;
    private readonly OpenAICompatibleClient openAICompatibleClient;

    public AgentExecutionRuntime(
        IEnumerable<Agent> agents,
        OpenAIResponsesClient openAIResponsesClient,
        OpenAICompatibleClient openAICompatibleClient)
    {
        this.agents = agents;
        this.openAIResponsesClient = openAIResponsesClient;
        this.openAICompatibleClient = openAICompatibleClient;
    }

    /// <summary>
    /// Agent cevabını agent kimliğine göre tek seferlik sonuç olarak üretir.
    /// </summary>
    public Task<AgentExecutionResult> ExecuteAsync(
        string agentId,
        string input,
        CancellationToken cancellationToken = default)
    {
        var agent = FindAgent(agentId);

        if (agent is null)
        {
            return Task.FromResult(AgentExecutionResult.Failure(
                errorCode: "AgentNotFound",
                errorMessage: $"Agent '{agentId}' was not found."));
        }

        return ExecuteAsync(agent, input, cancellationToken);
    }

    /// <summary>
    /// Agent cevabını tek seferlik sonuç olarak üretir.
    /// </summary>
    public async Task<AgentExecutionResult> ExecuteAsync(
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

        var validationFailure = ValidateProviderRuntime(agent);

        if (validationFailure is not null)
        {
            return validationFailure;
        }

        var endpoint = ProviderDefaults.ResolveUrl(agent);
        var providerDefault = ProviderDefaults.Get(agent.ProviderName);

        return providerDefault.Protocol switch
        {
            ProviderProtocol.OpenAICompatible => await ExecuteOpenAICompatibleAsync(
                agent,
                endpoint,
                input,
                cancellationToken),

            ProviderProtocol.Ollama => await ExecuteOllamaAsync(
                agent,
                endpoint,
                input,
                cancellationToken),

            _ => AgentExecutionResult.Failure(
                errorCode: "UnsupportedProviderProtocol",
                errorMessage: $"Provider protocol '{providerDefault.Protocol}' is not supported.")
        };
    }

    /// <summary>
    /// Agent cevabını agent kimliğine göre event stream olarak üretir.
    /// </summary>
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

        await foreach (var executionEvent in ExecuteStreamAsync(
            agent,
            input,
            toolInvoker,
            cancellationToken))
        {
            yield return executionEvent;
        }
    }


    /// <summary>
    /// Agent cevabını event stream olarak üretir.
    /// </summary>
    public async IAsyncEnumerable<AgentExecutionEvent> ExecuteStreamAsync(
        Agent agent,
        string input,
        AgentToolInvoker? toolInvoker = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (string.IsNullOrWhiteSpace(input))
        {
            yield break;
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

    private Task<AgentExecutionResult> ExecuteOpenAICompatibleAsync(
        Agent agent,
        Uri endpoint,
        string input,
        CancellationToken cancellationToken)
    {
        if (IsNativeOpenAIProvider(agent))
        {
            return openAIResponsesClient.ExecuteAsync(
                agent: agent,
                endpoint: endpoint,
                input: input,
                cancellationToken: cancellationToken);
        }

        return openAICompatibleClient.ExecuteAsync(
            agent: agent,
            endpoint: endpoint,
            input: input,
            cancellationToken: cancellationToken);
    }

    private async IAsyncEnumerable<AgentExecutionEvent> ExecuteOpenAICompatibleStreamAsync(
        Agent agent,
        Uri endpoint,
        string input,
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
                           cancellationToken: cancellationToken))
        {
            yield return executionEvent;
        }
    }

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

    private static bool IsNativeOpenAIProvider(Agent agent)
    {
        return string.Equals(
            agent.ProviderName,
            "openai",
            StringComparison.OrdinalIgnoreCase);
    }

    private Agent? FindAgent(string agentId)
    {
        return agents.FirstOrDefault(agent =>
            string.Equals(agent.Id, agentId, StringComparison.OrdinalIgnoreCase));
    }
}