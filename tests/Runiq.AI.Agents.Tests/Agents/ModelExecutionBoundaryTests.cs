using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Reranking;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Reranking;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class ModelExecutionBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Verifies a cancelled retrieval cannot start reranking or model work even if it returns usable candidates.
    public async Task RagCancellation_StopsRerankingAndModel(bool preCancelled)
    {
        using var state = new Probe();
        await using var provider = new ServiceCollection().AddSingleton(state).BuildServiceProvider();
        var agent = CreateAgent(false).UseRag(options =>
        {
            options.IndexName = "documents";
            options.Reranking.Enabled = true;
        });
        var retrieval = new CancellingRetriever(state);
        var reranker = new UnexpectedReranker();
        var runtime = new AgentExecutionRuntime([agent], new Resolver(new Client(state)),
            new AgentToolInvoker(provider), retrieval, reranker);
        if (preCancelled) state.Cancellation.Cancel();
        await Assert.ThrowsAsync<AgentRunCanceledException>(() => runtime.ExecuteAsync("agent", "question", state.Cancellation.Token));
        Assert.Equal(preCancelled ? 0 : 1, retrieval.Calls);
        Assert.Equal(0, reranker.Calls);
        Assert.Equal(0, state.ProviderCalls);
    }

    [Theory]
    [InlineData(false, false, "resolver")]
    [InlineData(false, true, "resolver")]
    [InlineData(true, false, "resolver")]
    [InlineData(true, true, "resolver")]
    [InlineData(false, false, "clients")]
    [InlineData(false, true, "clients")]
    [InlineData(true, false, "clients")]
    [InlineData(true, true, "clients")]
    [InlineData(false, false, "hosting")]
    [InlineData(false, true, "hosting")]
    [InlineData(true, false, "hosting")]
    [InlineData(true, true, "hosting")]
    // Verifies both model definition forms and APIs share one tool loop through both manual constructors and scoped hosting.
    public async Task ModelLoop_PreservesSingleToolInvocation(bool fluent, bool streaming, string construction)
    {
        using var state = new Probe();
        var agent = CreateAgent(fluent);
        var client = new Client(state);
        var services = new ServiceCollection().AddSingleton(state).AddSingleton(agent);
        services.AddScoped<IChatClientResolver>(_ => new Resolver(client));
        services.AddRuniqAgentServer();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var invoker = scope.ServiceProvider.GetRequiredService<AgentToolInvoker>();
        var runtime = construction switch
        {
            "resolver" => new AgentExecutionRuntime([agent], new Resolver(client), invoker),
            "clients" => new AgentExecutionRuntime([agent], client, client, invoker),
            "hosting" => scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>(),
            _ => throw new ArgumentOutOfRangeException(nameof(construction))
        };
        if (streaming)
        {
            var events = new List<AgentExecutionEvent>();
            await foreach (var item in runtime.ExecuteStreamAsync("agent", "question", cancellationToken: state.Cancellation.Token))
                events.Add(item);
            Assert.Equal("answer", events[^1].Message);
            Assert.Single(events, item => item.Status == AgentRunStatus.Completed);
        }
        else
        {
            var result = await runtime.ExecuteAsync("agent", "question", state.Cancellation.Token);
            Assert.True(result.IsSuccess);
            Assert.Equal("answer", result.Message);
        }
        Assert.Equal(2, state.ProviderCalls);
        Assert.Equal(1, state.ToolCalls);
        Assert.Equal(1, state.ToolDisposals);
        Assert.Equal(2, state.StreamDisposals);
    }

    [Theory]
    [InlineData("before")]
    [InlineData("resolver")]
    [InlineData("provider")]
    [InlineData("tool-return")]
    [InlineData("tool-throw")]
    // Verifies cancellation stops subsequent side effects even when a dependency returns normally after cancelling.
    public async Task Cancellation_StopsNewOperations(string boundary)
    {
        using var state = new Probe { Boundary = boundary };
        await using var provider = new ServiceCollection().AddSingleton(state).BuildServiceProvider();
        var runtime = new AgentExecutionRuntime([CreateAgent(false)], new Resolver(new Client(state), state), new AgentToolInvoker(provider));
        if (boundary == "before") state.Cancellation.Cancel();
        var exception = await Assert.ThrowsAsync<AgentRunCanceledException>(() =>
            runtime.ExecuteAsync("agent", "question", state.Cancellation.Token));
        Assert.Equal(AgentRunStatus.Cancelled, exception.Run.Status);
        Assert.Equal(state.Cancellation.Token, exception.CancellationToken);
        Assert.Equal(boundary is "before" or "resolver" ? 0 : 1, state.ProviderCalls);
        Assert.Equal(boundary.StartsWith("tool-") ? 1 : 0, state.ToolCalls);
        Assert.Equal(state.ToolCalls, state.ToolDisposals);
        Assert.Equal(state.ProviderCalls, state.StreamDisposals);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("tool-throw")]
    [InlineData("cleanup-cancel")]
    [InlineData("cleanup-fail")]
    [InlineData("execution-and-cleanup-fail")]
    // Verifies invocation-owned tools are disposed once and cleanup cannot hide cancellation or leak diagnostics.
    public async Task ToolOwnership_PreservesOutcome(string boundary)
    {
        using var state = new Probe { Boundary = boundary };
        await using var provider = new ServiceCollection().AddSingleton(state).BuildServiceProvider();
        var invoker = new AgentToolInvoker(provider);
        if (boundary is "tool-throw" or "cleanup-cancel")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invoker.InvokeAsync(CreateAgent(false), "probe", "{}", state.Cancellation.Token));
        else
        {
            var result = await invoker.InvokeAsync(CreateAgent(false), "probe", "{}", state.Cancellation.Token);
            Assert.Equal(boundary == "success", result.IsSuccess);
            if (!result.IsSuccess)
            {
                Assert.Equal("ToolExecutionFailed", result.ErrorCode);
                Assert.Equal("The tool could not be executed.", result.ErrorMessage);
            }
        }
        Assert.Equal(1, state.ToolCalls);
        Assert.Equal(1, state.ToolDisposals);
    }

    private static Agent CreateAgent(bool fluent) => (fluent
        ? new Agent("agent", "Agent", "instructions").UseModel("openai/model", "key")
        : new Agent("agent", "Agent", "instructions", "openai/model", "key")).AddTool<ProbeTool>();

    private sealed class Probe : IDisposable
    {
        internal string Boundary { get; init; } = "success";
        internal CancellationTokenSource Cancellation { get; } = new();
        internal int ProviderCalls, ToolCalls, ToolDisposals, StreamDisposals;
        public void Dispose() => Cancellation.Dispose();
    }

    [RuniqTool("probe", "Records a side effect.")]
    private sealed class ProbeTool(Probe state) : IRuniqTool<Dictionary<string, string>, string>, IAsyncDisposable
    {
        public Task<string> ExecuteAsync(Dictionary<string, string> input, CancellationToken cancellationToken = default)
        {
            Assert.Equal(state.Cancellation.Token, cancellationToken);
            state.ToolCalls++;
            if (state.Boundary.StartsWith("tool-")) state.Cancellation.Cancel();
            if (state.Boundary == "tool-throw") throw new OperationCanceledException(cancellationToken);
            if (state.Boundary == "execution-and-cleanup-fail") throw new InvalidOperationException("secret execution detail");
            return Task.FromResult("output");
        }
        public ValueTask DisposeAsync()
        {
            state.ToolDisposals++;
            if (state.Boundary == "cleanup-cancel") state.Cancellation.Cancel();
            if (state.Boundary.Contains("fail")) throw new InvalidOperationException("secret cleanup detail");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Resolver(IChatClient client, Probe? state = null) : IChatClientResolver
    {
        public IChatClient Resolve(ChatRequest request)
        {
            if (state?.Boundary == "resolver") state.Cancellation.Cancel();
            return client;
        }
    }

    private sealed class CancellingRetriever(Probe state) : IRagRetriever
    {
        internal int Calls;
        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(RagQuery query, CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.Equal(state.Cancellation.Token, cancellationToken);
            state.Cancellation.Cancel();
            return Task.FromResult<IReadOnlyList<RagSearchResult>>([new RagSearchResult
            {
                Chunk = new RagChunk { Id = "chunk", DocumentId = "document", Content = "usable context" },
                RawScore = 0.9, Metric = RagScoreMetrics.CosineSimilarity, HigherIsBetter = true
            }]);
        }
    }

    private sealed class UnexpectedReranker : IRagReranker
    {
        internal int Calls;
        public Task<RagRerankResult> RerankAsync(RagRerankRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Reranker must not start after cancellation.");
        }
    }

    private sealed class Client(Probe state) : IChatClient
    {
        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Batch must consume the common stream.");
        public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(ChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Assert.Equal(state.Cancellation.Token, cancellationToken);
            state.ProviderCalls++;
            try
            {
                if (state.ProviderCalls == 1)
                {
                    yield return new(ChatStreamingUpdateKind.ToolCallDelta, ToolCall: new ChatToolCall("call-1", "probe", "{}"));
                    if (state.Boundary.StartsWith("tool-"))
                        yield return new(ChatStreamingUpdateKind.ToolCallDelta, ToolCall: new ChatToolCall("call-2", "probe", "{}"));
                    if (state.Boundary == "provider") state.Cancellation.Cancel();
                }
                else yield return new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: "answer");
                await Task.CompletedTask;
            }
            finally { state.StreamDisposals++; }
        }
    }
}
