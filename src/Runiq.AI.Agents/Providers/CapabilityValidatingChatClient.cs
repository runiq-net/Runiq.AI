using Runiq.AI.Core.AI.Capabilities;
using Runiq.AI.Core.AI.Chat;

namespace Runiq.AI.Agents.Providers;

/// <summary>
/// Applies Core capability validation immediately before delegating to a provider client.
/// </summary>
internal sealed class CapabilityValidatingChatClient : IChatClient
{
    private readonly IChatClient inner;
    private readonly IModelCapabilityResolver capabilityResolver;

    /// <summary>Initializes the validating wrapper.</summary>
    /// <param name="inner">The provider client invoked after validation succeeds.</param>
    /// <param name="capabilityResolver">The resolver for effective model capabilities.</param>
    public CapabilityValidatingChatClient(IChatClient inner, IModelCapabilityResolver capabilityResolver)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.capabilityResolver = capabilityResolver ?? throw new ArgumentNullException(nameof(capabilityResolver));
    }

    /// <inheritdoc />
    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ModelCapabilityValidator.ValidateChat(capabilityResolver.Resolve(request.Model), request);
        return inner.CompleteAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ModelCapabilityValidator.ValidateStreaming(capabilityResolver.Resolve(request.Model), request);
        return inner.CompleteStreamingAsync(request, cancellationToken);
    }
}
