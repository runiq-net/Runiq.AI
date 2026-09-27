using System.Runtime.CompilerServices;
using Runiq.AI.Core.AI.Chat;

namespace Runiq.AI.Memory.PostgreSql.TestHost.Providers;

internal sealed class ProbeModel : IChatClientResolver, IChatClient
{
    internal IReadOnlyList<ChatMessage> Messages = [];
    internal int ToolCalls;
    /// <inheritdoc />
    public IChatClient Resolve(ChatRequest request) => this;
    /// <inheritdoc />
    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <inheritdoc />
    public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Messages = request.Messages.ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        if (!Messages.Any(m => m.Role == ChatRole.Tool))
            yield return new(ChatStreamingUpdateKind.ToolCallDelta, ToolCall: new("call", "lookup", "{}"));
        else yield return new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: "Ada");
    }
}

