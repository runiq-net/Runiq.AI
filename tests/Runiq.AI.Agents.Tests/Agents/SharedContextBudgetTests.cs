using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tests.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Metadata;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.DependencyInjection;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Services;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;
using Client = Runiq.AI.Agents.Tests.Agents.MemoryContinuationTests.Client;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class SharedContextBudgetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Initial overflow makes zero model calls, preserves one user input, and records a failed durable turn.
    public async Task FirstCallOverflow_IsSafeAndDurablyFailed(bool streaming)
    {
        var agent = Create().UseMemory().UseContextBudget(new(12, 1));
        var client = new Client();
        using var host = Services(agent, client).BuildServiceProvider();
        using var scope = host.CreateScope();
        var result = await Execute(scope, new("private query secret") { Memory = new("resource") }, streaming);
        Assert.Equal("ContextBudgetExceeded", result.ErrorCode);
        Assert.DoesNotContain("private", result.ErrorMessage);
        Assert.Contains("12", result.ErrorMessage);
        Assert.Empty(client.Requests);
        Assert.True(result.ContextBudget!.MandatoryPromptOverflow);
        Assert.Equal(1, result.ContextBudget.Invocation);
        var context = await Authorize(scope, result.ThreadId!);
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        Assert.Equal("private query secret", Assert.Single(await store.ReadMessagesAsync(context)).Content.Message.Content);
        Assert.Equal(MemoryTurnStatus.Failed, Assert.Single(await store.ReadTurnsAsync(context)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Near-limit calls select recent history under both limits while every excluded turn remains readable.
    public async Task RepeatedTurns_BoundRequestsWithoutDeletingHistory(bool streaming)
    {
        var agent = Create().UseMemory(new(history: new(2, 12))).UseContextBudget(new(24, 0));
        var client = new Client();
        using var host = Services(agent, client).BuildServiceProvider();
        using var scope = host.CreateScope();
        string? thread = null;
        AgentExecutionResult? last = null;
        for (var index = 0; index < 4; index++)
        {
            last = await Execute(scope, new("question" + index) { Memory = new("resource", thread) }, streaming);
            Assert.True(last.IsSuccess, last.ErrorMessage);
            thread = last.ThreadId;
        }
        Assert.Equal(new[] { "instructions", "question2", "answer", "question3" }, client.Requests[^1].Messages.Select(message => message.Content));
        Assert.All(client.Requests, request => AssertFits(request, 24, 0));
        Assert.Equal(2, last!.ContextBudget!.SelectedHistoryMessages);
        Assert.Equal(4, last.ContextBudget.ExcludedHistoryMessages);
        var stored = await scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>().ReadMessagesAsync(await Authorize(scope, thread!));
        Assert.Equal(8, stored.Count);
        Assert.Equal(new[] { "question0", "answer", "question1", "answer", "question2", "answer", "question3", "answer" },
            stored.Select(message => message.Content.Message.Content));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Large multiple tool results are persisted intact but block a second invocation when mandatory content overflows.
    public async Task ToolOverflow_StopsBeforeNextCallWithoutTruncation(bool streaming)
    {
        var state = new ToolState { Output = Words(500) };
        var agent = Create().UseMemory().AddTool<PayloadTool>().UseContextBudget(new(300, 10));
        var client = new Client { Updates = _ => Calls("a", "b") };
        using var host = Services(agent, client, state).BuildServiceProvider();
        using var scope = host.CreateScope();
        var result = await Execute(scope, new("query") { Memory = new("resource") }, streaming);
        Assert.Equal("ContextBudgetExceeded", result.ErrorCode);
        Assert.Single(client.Requests);
        Assert.Equal(2, state.Calls);
        Assert.Equal(2, result.ContextBudget!.Invocation);
        Assert.True(result.ContextBudget.MandatoryPromptOverflow);
        Assert.DoesNotContain(state.Output, result.ErrorMessage);
        var context = await Authorize(scope, result.ThreadId!);
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var stored = await store.ReadMessagesAsync(context);
        Assert.Equal(4, stored.Count);
        Assert.Equal(2, stored[1].Content.Message.ToolCalls!.Count);
        Assert.Equal("{}", stored[1].Content.Message.ToolCalls![0].ArgumentsJson);
        Assert.Equal(JsonSerializer.Serialize(state.Output), stored[2].Content.Message.Content);
        Assert.Equal(JsonSerializer.Serialize(state.Output), stored[3].Content.Message.Content);
        Assert.Equal(MemoryTurnStatus.Failed, Assert.Single(await store.ReadTurnsAsync(context)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Continuations evict optional history, retain exact active calls/results and never restore excluded context through a response chain.
    public async Task ToolContinuation_ReassessesHistoryEveryTime(bool streaming)
    {
        var state = new ToolState { Output = Words(80) };
        var agent = Create().UseMemory().AddTool<PayloadTool>().UseContextBudget(new(300, 10));
        var client = new Client { Updates = call => call == 2 ? Calls("a") :
            [new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: "answer", ProviderResponseId: "provider-response")] };
        using var host = Services(agent, client, state).BuildServiceProvider();
        using var scope = host.CreateScope();
        var first = await Execute(scope, new(Words(100)) { Memory = new("resource") }, streaming);
        Assert.True(first.IsSuccess, first.ErrorMessage);
        var second = await Execute(scope, new("query") { Memory = new("resource", first.ThreadId) }, streaming);
        Assert.True(second.IsSuccess, second.ErrorMessage);
        Assert.Equal(3, client.Requests.Count);
        Assert.Contains(client.Requests[1].Messages, message => message.Content == Words(100));
        Assert.DoesNotContain(client.Requests[2].Messages, message => message.Content == Words(100));
        Assert.Equal(JsonSerializer.Serialize(state.Output), client.Requests[2].Messages[^1].Content);
        Assert.Single(client.Requests[2].Messages[^2].ToolCalls!);
        Assert.All(client.Requests, request =>
        {
            AssertFits(request, 300, 10);
            Assert.False(request.Options!.Extensions.ContainsKey("previous_response_id"));
        });
        Assert.Equal(2, second.ContextBudget!.Invocation);
        Assert.True(second.ContextBudget.ExcludedHistoryMessages > 0);
        Assert.Equal(1, state.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Recomposition can drop source one without renumbering source two or validating a removed-source citation.
    public async Task RagContinuation_KeepsStableSourceNumbers(bool streaming)
    {
        var state = new ToolState { Output = Words(350) };
        var agent = Create().UseMemory().AddTool<PayloadTool>().UseRag(options =>
        {
            options.IndexName = "index";
            options.Mode = RagExecutionMode.Required;
            options.NoContextBehavior = RagNoContextBehavior.FailExecution;
        });
        var large = AgentContextAssemblerTests.Source("large", Words(180));
        var small = AgentContextAssemblerTests.Source("small", "small evidence");
        ChatMessage[] continuation = [new(ChatRole.User, "query"),
            new(ChatRole.Assistant, "", ToolCalls: [new("a", "payload", "{}")]),
            new(ChatRole.Tool, JsonSerializer.Serialize(state.Output), "a")];
        var probe = AgentContextAssembler.Assemble(agent, [], continuation, Tools(agent), new([small]), 2);
        var window = (int)probe.Budget.EstimatedPromptTokens + 10;
        agent.UseContextBudget(new(window, 10));
        var client = new Client { Updates = call => call == 1 ? Calls("a") :
            [new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: "removed [1], retained [2]")] };
        using var host = Services(agent, client, state).AddSingleton<IRagRetriever>(new Retriever([large, small])).BuildServiceProvider();
        using var scope = host.CreateScope();
        var result = await Execute(scope, new("query") { Memory = new("resource") }, streaming);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, client.Requests.Count);
        Assert.Contains("doc-large", client.Requests[0].Messages[2].Content);
        Assert.DoesNotContain("doc-large", client.Requests[1].Messages[2].Content);
        Assert.Contains("[2]", client.Requests[1].Messages[2].Content);
        Assert.DoesNotContain("[1]", client.Requests[1].Messages[2].Content);
        var citation = Assert.Single(result.Citations);
        Assert.Equal(2, citation.Number);
        Assert.Equal("doc-small", citation.DocumentId);
        Assert.Equal("small", Assert.Single(result.Rag!.ContextSelectedResults).Chunk.Id);
        Assert.Equal("large", Assert.Single(result.Rag.ContextExcludedResults).Result.Chunk.Id);
        Assert.All(client.Requests, request => AssertFits(request, window, 10));
        Assert.Equal(1, state.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Required grounding cannot use historical claims when a continuation leaves no space for accepted evidence.
    public async Task RequiredEvidenceExhaustion_StopsContinuation(bool returnNotFound)
    {
        var state = new ToolState { Output = Words(350) };
        var agent = Create().UseMemory().AddTool<PayloadTool>().UseRag(options =>
        {
            options.IndexName = "index";
            options.Mode = RagExecutionMode.Required;
            options.NoContextBehavior = returnNotFound ? RagNoContextBehavior.ReturnNotFound : RagNoContextBehavior.FailExecution;
        });
        ChatMessage[] continuation = [new(ChatRole.User, "query"),
            new(ChatRole.Assistant, "", ToolCalls: [new("a", "payload", "{}")]),
            new(ChatRole.Tool, JsonSerializer.Serialize(state.Output), "a")];
        var probe = AgentContextAssembler.Assemble(agent, [], continuation, Tools(agent), new(), 2);
        agent.UseContextBudget(new(probe.Budget.MandatoryPromptTokens + 10, 10));
        var client = new Client { Updates = _ => Calls("a") };
        using var host = Services(agent, client, state).AddSingleton<IRagRetriever>(new Retriever([AgentContextAssemblerTests.Source("a", "evidence")])).BuildServiceProvider();
        using var scope = host.CreateScope();
        var result = await Execute(scope, new("query") { Memory = new("resource") }, true);
        Assert.Single(client.Requests);
        Assert.Equal(returnNotFound, result.IsSuccess);
        Assert.Equal(RagNoContextReason.ContextBudgetExhausted, result.Rag!.NoContextReason);
        Assert.True(result.Rag.ModelInvocationSkipped);
        Assert.False(result.ContextBudget!.MandatoryPromptOverflow);
        Assert.Equal(2, result.ContextBudget.Invocation);
        Assert.Empty(result.Citations);
        if (!returnNotFound) Assert.Equal("RagContextUnavailable", result.ErrorCode);
    }

    [Fact]
    // Even completed stored claims and citation-looking text cannot satisfy Required grounding without current evidence.
    public async Task RequiredWithoutEvidence_DoesNotPromoteMemoryClaims()
    {
        var agent = Create().UseMemory();
        var client = new Client { Updates = _ => [new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: "policy is approved [1]")] };
        using var host = Services(agent, client).AddSingleton<IRagRetriever>(new Retriever([])).BuildServiceProvider();
        using var scope = host.CreateScope();
        var first = await Execute(scope, new("policy claim [1]") { Memory = new("resource") }, false);
        agent.UseRag(options =>
        {
            options.IndexName = "index";
            options.Mode = RagExecutionMode.Required;
            options.NoContextBehavior = RagNoContextBehavior.FailExecution;
        });
        var result = await Execute(scope, new("query") { Memory = new("resource", first.ThreadId) }, false);
        Assert.Equal("RagContextUnavailable", result.ErrorCode);
        Assert.Single(client.Requests);
        Assert.Empty(result.Rag!.ContextSelectedResults);
        Assert.Empty(result.Citations);
    }

    [Fact]
    // Cancellation after a persisted tool result prevents the pending model call and never fabricates completion.
    public async Task CancellationBeforeContinuation_PreservesCancelledTurn()
    {
        var agent = Create().UseMemory().AddTool<PayloadTool>();
        var client = new Client { Updates = _ => Calls("a") };
        using var host = Services(agent, client).BuildServiceProvider();
        using var scope = host.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        string? thread = null;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in runtime.ExecuteStreamAsync("agent", new AgentQuery("query") { Memory = new("resource") }, cancellationToken: cancellation.Token))
            {
                thread = item.ThreadId;
                Assert.NotEqual(AgentExecutionEventKind.Completed, item.Kind);
                if (item.Kind == AgentExecutionEventKind.ToolCallCompleted) cancellation.Cancel();
            }
        });
        Assert.Single(client.Requests);
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        Assert.Equal(MemoryTurnStatus.Cancelled, Assert.Single(await store.ReadTurnsAsync(await Authorize(scope, thread!))).Status);
    }

    private static Agent Create() => new("agent", "Agent", "instructions", "openai/model", "key");
    private static string Words(int count) => string.Join(' ', Enumerable.Repeat("word", count));
    private static IReadOnlyList<ChatStreamingUpdate> Calls(params string[] ids) => ids.Select(id =>
        new ChatStreamingUpdate(ChatStreamingUpdateKind.ToolCallDelta, ToolCall: new(id, "payload", "{}"), ProviderResponseId: "previous-response")).ToArray();
    private static ChatToolDefinition[] Tools(Agent agent) => agent.Tools.Select(tool => new ChatToolDefinition(
        tool.Name, tool.Description, JsonSerializer.Serialize(ToolJsonSchemaGenerator.CreateSchema(tool.InputType)))).ToArray();
    private static void AssertFits(ChatRequest request, int window, int reserve) => Assert.True(
        request.Messages.Sum(ContextTokenEstimator.EstimateMessage) + ContextTokenEstimator.EstimateTools(request.Tools!) + reserve <= window);

    private static IServiceCollection Services(Agent agent, Client client, ToolState? state = null) => new ServiceCollection()
        .AddLogging().AddRuniqServer(options => options.AddAgent(agent)).AddRuniqMemoryInMemory()
        .AddSingleton(state ?? new ToolState()).AddScoped<IMemoryIdentityResolver, MemoryContinuationTests.Identity>()
        .AddScoped<IMemoryAccessPolicy, MemoryContinuationTests.Policy>().AddSingleton<IChatClientResolver>(client);

    private static async Task<MemoryContext> Authorize(IServiceScope scope, string thread) =>
        (await scope.ServiceProvider.GetRequiredService<MemoryAuthorizationService>().AuthorizeAsync(
            new("caller", "tenant"), new("resource", thread), "agent", new()))!;

    private static async Task<AgentExecutionResult> Execute(IServiceScope scope, AgentQuery query, bool streaming)
    {
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        if (!streaming) return await runtime.ExecuteAsync("agent", query);
        var builder = new AgentExecutionResultBuilder();
        await foreach (var item in runtime.ExecuteStreamAsync("agent", query)) builder.Apply(item);
        return builder.Build();
    }

    private sealed class ToolState { internal string Output = "result"; internal int Calls; }

    [RuniqTool("payload", "Returns a payload.")]
    private sealed class PayloadTool(ToolState state) : IRuniqTool<Dictionary<string, string>, string>
    {
        public Task<string> ExecuteAsync(Dictionary<string, string> input, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state.Calls++;
            return Task.FromResult(state.Output);
        }
    }

    private sealed class Retriever(IReadOnlyList<RagSearchResult> results) : IRagRetriever
    {
        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(RagQuery query, CancellationToken cancellationToken = default) => Task.FromResult(results);
    }
}
