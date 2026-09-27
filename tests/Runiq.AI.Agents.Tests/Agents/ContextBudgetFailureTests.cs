using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Chat;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class ContextBudgetFailureTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    // Empty completions and pre-delta provider failures retain the actual latest call's budget for both entry points.
    public async Task Failure_RetainsCurrentInvocationBudget(bool providerFailure, bool afterTool, bool streaming)
    {
        var client = new Client(providerFailure, afterTool);
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = new Agent("agent", "Agent", "instructions", "openai/model", "key").AddTool<LookupTool>();
        var runtime = new AgentExecutionRuntime([agent], client, new AgentToolInvoker(services));
        AgentExecutionResult result;
        if (streaming)
        {
            var builder = new AgentExecutionResultBuilder();
            AgentExecutionEvent? terminal = null;
            await foreach (var item in runtime.ExecuteStreamAsync("agent", "question")) { builder.Apply(item); terminal = item; }
            Assert.Equal(afterTool ? 2 : 1, terminal!.ContextBudget!.Invocation);
            result = builder.Build();
        }
        else result = await runtime.ExecuteAsync("agent", "question");
        Assert.Equal(providerFailure ? "AgentExecutionFailed" : "AgentExecutionEmptyMessage", result.ErrorCode);
        Assert.Equal(afterTool ? 2 : 1, client.Requests.Count);
        var budget = Assert.IsType<AgentContextBudgetMetadata>(result.ContextBudget);
        Assert.Equal(client.Requests.Count, budget.Invocation);
        var last = client.Requests[^1];
        Assert.Equal(last.Messages.Sum(ContextTokenEstimator.EstimateMessage) + ContextTokenEstimator.EstimateTools(last.Tools!), budget.MandatoryPromptTokens);
        if (afterTool)
            Assert.True(budget.MandatoryPromptTokens > client.Requests[0].Messages.Sum(ContextTokenEstimator.EstimateMessage) + ContextTokenEstimator.EstimateTools(last.Tools!));
        Assert.DoesNotContain("provider-secret", result.ErrorMessage);
    }

    [Fact]
    // A request rejected before model execution has no fabricated accounting, even after another request used the same runtime.
    public async Task NoModelCall_DoesNotReusePriorDiagnostics()
    {
        var client = new Client(false, false);
        using var services = new ServiceCollection().BuildServiceProvider();
        var runtime = new AgentExecutionRuntime([new Agent("agent", "Agent", "instructions", "openai/model", "key")], client, new AgentToolInvoker(services));
        Assert.NotNull((await runtime.ExecuteAsync("agent", "question")).ContextBudget);
        var result = await runtime.ExecuteAsync("agent", " ");
        Assert.Equal("InputRequired", result.ErrorCode);
        Assert.Null(result.ContextBudget);
        Assert.Single(client.Requests);
    }

    [RuniqTool("lookup", "Returns a value.")]
    private sealed class LookupTool : IRuniqTool<Dictionary<string, string>, string>
    {
        public Task<string> ExecuteAsync(Dictionary<string, string> input, CancellationToken cancellationToken = default) => Task.FromResult("tool result");
    }
    private sealed class Client(bool providerFailure, bool afterTool) : IChatClientResolver, IChatClient
    {
        internal List<ChatRequest> Requests { get; } = [];
        public IChatClient Resolve(ChatRequest request) => this;
        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(ChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (afterTool && Requests.Count == 1)
            {
                yield return new(ChatStreamingUpdateKind.ToolCallDelta, ToolCall: new("call", "lookup", "{}"));
                yield break;
            }
            if (providerFailure) throw new InvalidOperationException("provider-secret");
            yield return new(ChatStreamingUpdateKind.Completed);
        }
    }
}
