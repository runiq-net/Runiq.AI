using System.Runtime.CompilerServices;
using Runiq.AI.Core.AI.Chat;

namespace Runiq.AI.MemoryConversation.Providers;

// A deterministic model boundary makes the example runnable without credentials or paid API calls.
internal sealed class DeterministicModel : IChatClientResolver, IChatClient
{
    /// <inheritdoc />
    public IChatClient Resolve(ChatRequest request) => this;
    /// <inheritdoc />
    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The runtime uses streaming for both execution APIs.");

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        var previousName = request.Messages.FirstOrDefault(m => m.Role == ChatRole.User && m.Content == "My name is Ada.");
        var isContinuation = request.Messages.Count(m => m.Role == ChatRole.User) > 1;
        var answer = isContinuation
            ? previousName is not null && request.Messages.Any(m => m.Role == ChatRole.Assistant)
                ? "Your name is Ada." : throw new InvalidOperationException("Previous conversation was not supplied.")
            : "Hello, Ada.";
        yield return new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: answer);
    }
}
