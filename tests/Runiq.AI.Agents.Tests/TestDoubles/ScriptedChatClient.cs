using System.Runtime.CompilerServices;
using Runiq.AI.Core.AI.Chat;

namespace Runiq.AI.Agents.Tests.TestDoubles;

internal sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<ChatResponse> responses;
    private readonly Queue<IReadOnlyList<ChatStreamingUpdate>> streams;

    public ScriptedChatClient(
        IEnumerable<ChatResponse>? responses = null,
        IEnumerable<IReadOnlyList<ChatStreamingUpdate>>? streams = null)
    {
        this.responses = new Queue<ChatResponse>(responses ?? []);
        this.streams = new Queue<IReadOnlyList<ChatStreamingUpdate>>(streams ?? []);
    }

    public List<ChatRequest> Requests { get; } = [];

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return Task.FromResult(responses.Count > 0
            ? responses.Dequeue()
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."), ChatFinishReason.Stop));
    }

    public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);

        var updates = streams.Count > 0
            ? streams.Dequeue()
            :
            [
                new ChatStreamingUpdate(ChatStreamingUpdateKind.ContentDelta, ContentDelta: "Test response."),
                new ChatStreamingUpdate(ChatStreamingUpdateKind.Completed, FinishReason: ChatFinishReason.Stop)
            ];

        foreach (var update in updates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
            await Task.Yield();
        }
    }
}

internal sealed class TestChatClientResolver : IChatClientResolver
{
    public TestChatClientResolver(IChatClient? client = null)
    {
        Client = client ?? new ScriptedChatClient();
    }

    public IChatClient Client { get; }

    public List<ChatRequest> Requests { get; } = [];

    public IChatClient Resolve(ChatRequest request)
    {
        Requests.Add(request);
        return Client;
    }
}
