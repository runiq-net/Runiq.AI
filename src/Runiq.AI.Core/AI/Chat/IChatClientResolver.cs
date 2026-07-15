namespace Runiq.AI.Core.AI.Chat;

/// <summary>
/// Resolves the provider-neutral chat client that should handle a model request.
/// </summary>
public interface IChatClientResolver
{
    /// <summary>
    /// Resolves a chat client for the supplied request.
    /// </summary>
    /// <param name="request">The request whose provider and model determine the client.</param>
    /// <returns>The chat client registered for the request.</returns>
    /// <exception cref="NotSupportedException">Thrown when no client supports the configured provider protocol.</exception>
    IChatClient Resolve(ChatRequest request);
}
