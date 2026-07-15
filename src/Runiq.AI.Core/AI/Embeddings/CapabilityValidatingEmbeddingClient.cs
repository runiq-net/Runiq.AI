using Runiq.AI.Core.AI.Capabilities;

namespace Runiq.AI.Core.AI.Embeddings;

/// <summary>
/// Validates model capabilities and embedding dimensions before delegating to an embedding provider client.
/// </summary>
public sealed class CapabilityValidatingEmbeddingClient : IEmbeddingClient
{
    private readonly IEmbeddingClient inner;
    private readonly IModelCapabilityResolver capabilityResolver;

    /// <summary>
    /// Initializes a validating embedding client. Both dependencies must be thread-safe when this instance is shared.
    /// </summary>
    /// <param name="inner">The provider client invoked only after validation succeeds.</param>
    /// <param name="capabilityResolver">The resolver that supplies the effective model descriptor.</param>
    public CapabilityValidatingEmbeddingClient(IEmbeddingClient inner, IModelCapabilityResolver capabilityResolver)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.capabilityResolver = capabilityResolver ?? throw new ArgumentNullException(nameof(capabilityResolver));
    }

    /// <inheritdoc />
    public Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        ModelCapabilityValidator.ValidateEmbedding(capabilityResolver.Resolve(request.Model), request);
        return inner.EmbedAsync(request, cancellationToken);
    }
}
