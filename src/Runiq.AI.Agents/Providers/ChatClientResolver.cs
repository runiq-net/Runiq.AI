using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.AI.Capabilities;
using Runiq.AI.Core.Providers;

namespace Runiq.AI.Agents.Providers;

/// <summary>
/// Selects one of the registered protocol clients without exposing provider clients to the agent runtime.
/// </summary>
internal sealed class ChatClientResolver : IChatClientResolver
{
    private readonly OpenAI.OpenAIResponsesClient responsesClient;
    private readonly OpenAI.OpenAICompatibleClient compatibleClient;
    private readonly IModelCapabilityResolver capabilityResolver;

    /// <summary>
    /// Initializes a resolver over the registered Responses and OpenAI-compatible clients.
    /// </summary>
    /// <param name="responsesClient">The native OpenAI Responses protocol client.</param>
    /// <param name="compatibleClient">The OpenAI-compatible chat completions client.</param>
    /// <param name="capabilityResolver">The Core resolver used to validate the selected model before invocation.</param>
    public ChatClientResolver(
        OpenAI.OpenAIResponsesClient responsesClient,
        OpenAI.OpenAICompatibleClient compatibleClient,
        IModelCapabilityResolver capabilityResolver)
    {
        this.responsesClient = responsesClient;
        this.compatibleClient = compatibleClient;
        this.capabilityResolver = capabilityResolver;
    }

    /// <inheritdoc />
    public IChatClient Resolve(ChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.Equals(request.Model.ProviderName, "openai", StringComparison.OrdinalIgnoreCase))
        {
            return new CapabilityValidatingChatClient(responsesClient, capabilityResolver);
        }

        var protocol = ProviderDefaults.Get(request.Model.ProviderName).Protocol;

        return protocol switch
        {
            ProviderProtocol.OpenAICompatible or ProviderProtocol.Ollama => new CapabilityValidatingChatClient(compatibleClient, capabilityResolver),
            _ => throw new NotSupportedException($"Provider protocol '{protocol}' is not supported.")
        };
    }
}
