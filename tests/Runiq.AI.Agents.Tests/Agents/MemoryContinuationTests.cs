using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Memory.Services;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.DependencyInjection;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class MemoryContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Both entry points expose the created thread and send each previous/current message exactly once.
    public async Task TwoTurns_ReplayAuthorizedHistory(bool streaming)
    {
        var client = new Client();
        using var host = Services(client).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = host.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var first = await Execute(runtime, new("My name is Ada") { Memory = new("resource") }, streaming);
        Assert.True(first.IsSuccess, first.ErrorCode);
        Assert.NotNull(first.ThreadId);
        var second = await Execute(runtime, new("What is my name?") { Memory = new("resource", first.ThreadId) }, streaming);
        Assert.True(second.IsSuccess, second.ErrorCode);
        Assert.Equal(first.ThreadId, second.ThreadId);
        Assert.NotEqual(first.RunId, second.RunId);
        Assert.Equal(new[] { "instructions", "My name is Ada", "answer", "What is my name?" }, client.Requests[1].Messages.Select(m => m.Content));
        var isolated = await Execute(runtime, new("separate") { Memory = new("resource") }, streaming);
        Assert.NotEqual(first.ThreadId, isolated.ThreadId);
        Assert.Equal(new[] { "instructions", "separate" }, client.Requests[2].Messages.Select(m => m.Content));
        scope.ServiceProvider.GetRequiredService<Identity>().Value = new("caller", "other-tenant");
        var denied = await runtime.ExecuteAsync("agent", new AgentQuery("steal") { Memory = new("resource", first.ThreadId) });
        Assert.Equal("MemoryAccessDenied", denied.ErrorCode);
        Assert.Equal(3, client.Requests.Count);
    }

    [Fact]
    // Full paging preserves a stable history boundary even when the transcript exceeds the store's default page.
    public async Task History_LoadsBeyondFirstPage()
    {
        var client = new Client();
        using var host = Services(client).BuildServiceProvider();
        using var scope = host.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        string? thread = null;
        for (var i = 0; i < 52; i++)
        {
            var result = await runtime.ExecuteAsync("agent", new AgentQuery($"question-{i}") { Memory = new("resource", thread) });
            Assert.True(result.IsSuccess, result.ErrorCode);
            thread = result.ThreadId;
        }
        Assert.Equal(104, client.Requests[^1].Messages.Count);
        Assert.Equal("question-0", client.Requests[^1].Messages[1].Content);
        Assert.Equal("question-51", client.Requests[^1].Messages[^1].Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Multiple tool rounds retain exact calls, text, and success/failure payloads without executing historical tools.
    public async Task ToolHistory_ReplaysExactModelMessages(bool streaming)
    {
        var client = new Client
        {
            Updates = call => call switch
            {
                1 => [new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: "checking"),
                    new(ChatStreamingUpdateKind.ToolCallDelta, ToolCall: new("one", "echo", "{ \"value\": \"first\" }")),
                    new(ChatStreamingUpdateKind.ToolCallDelta, ToolCall: new("two", "missing", "{}"))],
                2 => [new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: "again"),
                    new(ChatStreamingUpdateKind.ToolCallDelta, ToolCall: new("three", "echo", "{\"value\":\"second\"}"))],
                _ => [new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: "answer")]
            }
        };
        var counter = new ToolCounter();
        using var host = Services(client).AddSingleton(counter).BuildServiceProvider();
        using var scope = host.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var first = await Execute(runtime, new("tools") { Memory = new("resource") }, streaming);
        Assert.True(first.IsSuccess, first.ErrorCode);
        Assert.Equal("checkingagainanswer", first.Message);
        Assert.Equal(2, counter.Calls);
        var second = await Execute(runtime, new("continue") { Memory = new("resource", first.ThreadId) }, streaming);
        Assert.True(second.IsSuccess, second.ErrorCode);
        Assert.Equal(2, counter.Calls);
        var history = client.Requests[^1].Messages;
        Assert.Equal(new[] { ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Tool,
            ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant, ChatRole.User }, history.Select(m => m.Role));
        Assert.Equal("checking", history[2].Content);
        Assert.Equal("{ \"value\": \"first\" }", history[2].ToolCalls![0].ArgumentsJson);
        Assert.Equal("again", history[5].Content);
        Assert.Equal("answer", history[7].Content);
        Assert.Equal(client.Requests[1].Messages[3].Content, history[3].Content);
        Assert.Equal(client.Requests[1].Messages[4].Content, history[4].Content);
        Assert.Contains("false", history[4].Content);
        Assert.Equal("two", history[4].ToolCallId);
        var context = await scope.ServiceProvider.GetRequiredService<MemoryAuthorizationService>().AuthorizeAsync(
            new("caller", "tenant"), new("resource", first.ThreadId), "agent", new());
        var stored = await scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>().ReadMessagesAsync(context!);
        Assert.Equal(9, stored.Count);
        Assert.Equal(history[4].Content, stored[3].Content.Message.Content);
    }

    internal static IServiceCollection Services(Client client)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRuniqServer(o => o.AddAgent(new Agent("agent", "Agent", "instructions", "openai/model", "test-key").UseMemory().AddTool<EchoTool>()));
        services.AddRuniqMemoryInMemory();
        services.AddScoped<Identity>();
        services.AddScoped<IMemoryIdentityResolver>(p => p.GetRequiredService<Identity>());
        services.AddScoped<IMemoryAccessPolicy, Policy>();
        services.AddSingleton<IChatClientResolver>(client);
        return services;
    }

    private static async Task<AgentExecutionResult> Execute(AgentExecutionRuntime runtime, AgentQuery query, bool streaming)
    {
        if (!streaming) return await runtime.ExecuteAsync("agent", query);
        var builder = new AgentExecutionResultBuilder();
        var events = new List<AgentExecutionEvent>();
        await foreach (var item in runtime.ExecuteStreamAsync("agent", query)) { events.Add(item); builder.Apply(item); }
        Assert.Equal(AgentExecutionEventKind.ConversationStarted, events[0].Kind);
        Assert.NotNull(events[0].ThreadId);
        return builder.Build();
    }

    internal sealed class Identity : IMemoryIdentityResolver
    {
        internal MemoryIdentity Value = new("caller", "tenant");
        public ValueTask<MemoryIdentity?> ResolveAsync(CancellationToken cancellationToken) => ValueTask.FromResult<MemoryIdentity?>(Value);
    }

    internal sealed class Policy : IMemoryAccessPolicy
    {
        public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(resourceId == "resource");
    }

    internal sealed class ToolCounter { internal int Calls; internal CancellationTokenSource? Cancellation; }

    [RuniqTool("echo", "Returns the requested value.")]
    private sealed class EchoTool(ToolCounter counter) : IRuniqTool<Dictionary<string, string>, string>
    {
        public Task<string> ExecuteAsync(Dictionary<string, string> input, CancellationToken cancellationToken = default)
        {
            counter.Calls++;
            counter.Cancellation?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(input["value"]);
        }
    }

    internal sealed class Client : IChatClientResolver, IChatClient
    {
        internal List<ChatRequest> Requests = [];
        internal Action? AfterUpdate;
        internal Func<int, IReadOnlyList<ChatStreamingUpdate>> Updates = _ => [new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: "answer")];
        public IChatClient Resolve(ChatRequest request) => this;
        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(ChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request with { Messages = request.Messages.ToArray() });
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var update in Updates(Requests.Count))
            {
                yield return update;
                AfterUpdate?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
