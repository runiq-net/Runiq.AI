using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Providers;

namespace Runiq.AI.Agents.Providers;

/// <summary>
/// Selects one of the registered protocol clients without exposing provider clients to the agent runtime.
/// </summary>
internal sealed class ChatClientResolver : IChatClientResolver
{
    private readonly OpenAI.OpenAIResponsesClient responsesClient;
    private readonly OpenAI.OpenAICompatibleClient compatibleClient;

    /// <summary>
    /// Initializes a resolver over the registered Responses and OpenAI-compatible clients.
    /// </summary>
    /// <param name="responsesClient">The native OpenAI Responses protocol client.</param>
    /// <param name="compatibleClient">The OpenAI-compatible chat completions client.</param>
    public ChatClientResolver(
        OpenAI.OpenAIResponsesClient responsesClient,
        OpenAI.OpenAICompatibleClient compatibleClient)
    {
        this.responsesClient = responsesClient;
        this.compatibleClient = compatibleClient;
    }

    /// <inheritdoc />
    public IChatClient Resolve(ChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.Equals(request.Model.ProviderName, "openai", StringComparison.OrdinalIgnoreCase))
        {
            return responsesClient;
        }

        var protocol = ProviderDefaults.Get(request.Model.ProviderName).Protocol;

        return protocol switch
        {
            ProviderProtocol.OpenAICompatible or ProviderProtocol.Ollama => compatibleClient,
            _ => throw new NotSupportedException($"Provider protocol '{protocol}' is not supported.")
        };
    }
}
