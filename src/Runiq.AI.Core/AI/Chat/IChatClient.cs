namespace Runiq.AI.Core.AI.Chat;

/// <summary>
/// Invokes provider-neutral chat models for Runiq consumers.
/// </summary>
/// <remarks>
/// Implementations are expected to be thread-safe when registered as shared services unless their
/// documentation states otherwise. Callers own request instances, and implementations own any provider
/// resources created during an invocation.
/// </remarks>
public interface IChatClient
{
    /// <summary>
    /// Sends a chat request and returns the completed provider response.
    /// </summary>
    /// <param name="request">The validated chat request to send to the model provider.</param>
    /// <param name="cancellationToken">Cancels the provider request before the response is completed.</param>
    /// <returns>The completed chat response mapped to provider-neutral result fields.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ChatProviderException">Thrown when the provider rejects the request or returns a malformed response.</exception>
    Task<ChatResponse> CompleteAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chat request and yields provider-neutral streaming updates in provider order.
    /// </summary>
    /// <param name="request">The validated chat request to stream.</param>
    /// <param name="cancellationToken">Cancels the provider stream and disposes the underlying response resources.</param>
    /// <returns>Streaming updates in the same order in which the provider produced them.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ChatProviderException">Thrown when the provider rejects the request or returns a malformed stream event.</exception>
    IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default);
}
