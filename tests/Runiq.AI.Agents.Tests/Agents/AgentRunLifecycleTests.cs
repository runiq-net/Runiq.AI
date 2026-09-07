using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core.AI.Chat;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class AgentRunLifecycleTests
{
    [Fact]
    // Ensures the shared request retains the complete existing query and reusable definition.
    public void Request_PreservesAgentAndQueryOptions()
    {
        var agent = CreateAgent();
        var query = new AgentQuery("RunId=untrusted-input") { IndexName = "override-index" };
        var request = new AgentExecutionRequest(agent, query);
        Assert.Same(agent, request.Agent);
        Assert.Same(query, request.Query);
        Assert.Equal("override-index", request.Query.IndexName);
    }

    [Fact]
    // Forces overlapping calls using the same query and agent to prove run identity is invocation-owned.
    public async Task ConcurrentRuns_KeepEventsAndResultsIsolated()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var client = new ProbeClient(overlap: true);
        var agent = CreateAgent();
        var runtime = CreateRuntime(agent, client, services);
        var query = new AgentQuery("RunId=untrusted-input");
        var runs = await Task.WhenAll(
            CollectAsync(runtime.ExecuteStreamAsync(agent.Id, query)),
            CollectAsync(runtime.ExecuteStreamAsync(agent.Id, query))).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotEqual(runs[0][0].RunId, runs[1][0].RunId);
        foreach (var events in runs)
        {
            Assert.Equal(Enumerable.Range(1, events.Count).Select(number => (long?)number),
                events.Select(item => item.SequenceNumber));
            var runId = events[0].RunId;
            Assert.True(Guid.TryParseExact(runId, "N", out _));
            Assert.NotEqual(query.Message, runId);
            Assert.All(events, item =>
            {
                Assert.Equal(runId, item.RunId);
                Assert.Equal(agent.Id, item.AgentId);
                Assert.Null(item.ProviderSessionId);
            });
            Assert.Single(events, item => item.Status != AgentRunStatus.Running);
            Assert.Equal(AgentRunStatus.Completed, events[^1].Status);
            var builder = new AgentExecutionResultBuilder();
            events.ForEach(builder.Apply);
            var result = builder.Build();
            Assert.Equal(runId, result.RunId);
            Assert.Equal(agent.Id, result.AgentId);
            Assert.Equal("answer", result.Message);
            Assert.Equal(AgentRunStatus.Completed, result.Status);
        }
        var mixedBuilder = new AgentExecutionResultBuilder();
        mixedBuilder.Apply(runs[0][0]);
        Assert.Throws<InvalidOperationException>(() => mixedBuilder.Apply(runs[1][0]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    // Verifies every existing non-streaming overload produces a fresh correlated result.
    public async Task ExecuteOverloads_ReturnRunIdentity(int overload)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateAgent();
        var runtime = CreateRuntime(agent, new ProbeClient(), services);
        var query = new AgentQuery("question");
        var result = await (overload switch
        {
            0 => runtime.ExecuteAsync(agent.Id, query.Message),
            1 => runtime.ExecuteAsync(agent.Id, query),
            2 => runtime.ExecuteAsync(agent, query.Message),
            _ => runtime.ExecuteAsync(agent, query)
        });
        Assert.True(result.IsSuccess);
        Assert.True(Guid.TryParseExact(result.RunId, "N", out _));
        Assert.Equal(agent.Id, result.AgentId);
        Assert.Null(result.ProviderSessionId);
    }

    [Theory]
    [InlineData("empty", "AgentExecutionEmptyMessage")]
    [InlineData("throw", "AgentExecutionFailed")]
    [InlineData("provider-cancel", "AgentExecutionFailed")]
    [InlineData("cleanup-throw", "AgentExecutionFailed")]
    // Ensures both APIs agree on empty output, executor faults, provider cancellation and cleanup faults.
    public async Task Failures_ReachOneTerminalOutcome(string mode, string code)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateAgent();
        var runtime = CreateRuntime(agent, new ProbeClient(mode), services);
        var events = await CollectAsync(runtime.ExecuteStreamAsync(agent.Id, "question"));
        Assert.Single(events, item => item.Status != AgentRunStatus.Running);
        Assert.Equal(AgentRunStatus.Failed, events[^1].Status);
        Assert.Equal(code, events[^1].ErrorCode);
        Assert.All(events, item => Assert.Equal(events[0].RunId, item.RunId));
        var result = await runtime.ExecuteAsync(agent, "question");
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.NotNull(result.RunId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Ensures cancellation before execution yields no events or provider calls in either API.
    public async Task PreCancelledRun_ThrowsWithCancelledContext(bool streaming)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new ProbeClient();
        var agent = CreateAgent();
        var runtime = CreateRuntime(agent, client, services);
        var exception = await Assert.ThrowsAsync<AgentRunCanceledException>(async () =>
        {
            if (streaming)
                await CollectAsync(runtime.ExecuteStreamAsync(agent.Id, "question", cancellationToken: cancellation.Token));
            else
                await runtime.ExecuteAsync(agent, "question", cancellation.Token);
        });
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(agent.Id, exception.Run.AgentId);
        Assert.Equal(AgentRunStatus.Cancelled, exception.Run.Status);
        Assert.True(Guid.TryParseExact(exception.Run.RunId, "N", out _));
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    // Ensures enumerator-token cancellation preserves the identity of already published partial events.
    public async Task PartialStreamCancellation_PreservesIdentityAndDisposesProvider()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var client = new ProbeClient();
        var agent = CreateAgent();
        var runtime = CreateRuntime(agent, client, services);
        await using var enumerator = runtime.ExecuteStreamAsync(agent.Id, "question")
            .GetAsyncEnumerator(cancellation.Token);
        Assert.True(await enumerator.MoveNextAsync());
        var first = enumerator.Current;
        cancellation.Cancel();
        var exception = await Assert.ThrowsAsync<AgentRunCanceledException>(async () => await enumerator.MoveNextAsync());
        Assert.Equal(first.RunId, exception.Run.RunId);
        Assert.Equal(AgentRunStatus.Cancelled, exception.Run.Status);
        Assert.Equal(1, client.Disposals);
    }

    [Fact]
    // Ensures early disposal releases the provider and re-enumeration starts an independent run.
    public async Task StreamDisposal_ReleasesProviderAndReEnumerationCreatesNewRun()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var client = new ProbeClient();
        var agent = CreateAgent();
        var stream = CreateRuntime(agent, client, services).ExecuteStreamAsync(agent.Id, "question");
        Assert.Equal(0, client.Calls);
        var enumerator = stream.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        var runId = enumerator.Current.RunId;
        await enumerator.DisposeAsync();
        Assert.Equal(1, client.Disposals);
        var second = await CollectAsync(stream);
        Assert.NotEqual(runId, second[0].RunId);
        Assert.Equal(AgentRunStatus.Completed, second[^1].Status);
    }

    [Theory]
    [InlineData("missing", "question", "AgentNotFound")]
    [InlineData("agent", " ", "InputRequired")]
    // Ensures runtime validation failures also carry run identity through both public API shapes.
    public async Task ValidationFailures_AreCorrelated(string id, string input, string code)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var client = new ProbeClient();
        var runtime = CreateRuntime(CreateAgent(), client, services);
        var result = await runtime.ExecuteAsync(id, input);
        var terminal = Assert.Single(await CollectAsync(runtime.ExecuteStreamAsync(id, input)));
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(code, terminal.ErrorCode);
        Assert.NotNull(result.RunId);
        Assert.NotNull(terminal.RunId);
        Assert.Equal(id, result.AgentId);
        Assert.NotEqual(result.RunId, terminal.RunId);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    // Ensures standalone factories remain uncorrelated and cannot manufacture runtime identity.
    public void LegacyFactories_PreserveUncorrelatedUsage()
    {
        var builder = new AgentExecutionResultBuilder();
        builder.Apply(AgentExecutionEvent.AssistantDelta("answer"));
        builder.Apply(AgentExecutionEvent.Completed());
        Assert.Null(builder.Build().RunId);
        Assert.Null(AgentExecutionResult.Success("answer").RunId);
        Assert.Null(AgentExecutionResult.Failure("code", "message").AgentId);
        Assert.Equal(AgentRunStatus.Running, AgentExecutionEvent.ToolCallFailed("id", "tool", "error").Status);
    }

    [Fact]
    // Ensures a terminal run cannot be changed by a later cancellation or completion attempt.
    public void RunContext_AllowsOnlyOneTerminalTransition()
    {
        foreach (var terminal in new[] { AgentRunStatus.Completed, AgentRunStatus.Failed, AgentRunStatus.Cancelled })
        {
            var run = new AgentRunContext("agent");
            Assert.Equal(AgentRunStatus.Running, run.Status);
            Assert.Null(run.EndedAt);
            Assert.Equal(TimeSpan.Zero, run.StartedAt.Offset);
            run.Finish(terminal);
            var end = Assert.IsType<DateTimeOffset>(run.EndedAt);
            Assert.InRange(end, run.StartedAt, DateTimeOffset.UtcNow);
            foreach (var alternative in new[] { AgentRunStatus.Completed, AgentRunStatus.Failed, AgentRunStatus.Cancelled })
                run.Finish(alternative);
            Assert.Equal(terminal, run.Status);
            Assert.Equal(end, run.EndedAt);
        }
    }

    private static Agent CreateAgent() => new("agent", "Agent", "instructions", "openai/model", "key");

    private static AgentExecutionRuntime CreateRuntime(Agent agent, ProbeClient client, IServiceProvider services) =>
        new([agent], client, client, new AgentToolInvoker(services));

    private static async Task<List<AgentExecutionEvent>> CollectAsync(IAsyncEnumerable<AgentExecutionEvent> stream)
    {
        var events = new List<AgentExecutionEvent>();
        await foreach (var item in stream) events.Add(item);
        return events;
    }

    private sealed class ProbeClient(string mode = "success", bool overlap = false) : IChatClient
    {
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        internal int Disposals;

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(ChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref Calls) == 2) gate.TrySetResult();
            try
            {
                if (overlap) await gate.Task.WaitAsync(cancellationToken);
                if (mode == "throw") throw new InvalidOperationException("private provider detail");
                if (mode == "provider-cancel") throw new OperationCanceledException("provider timeout");
                yield return new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: mode == "empty" ? " \n" : "answer");
                await Task.Yield();
            }
            finally
            {
                Interlocked.Increment(ref Disposals);
                if (mode == "cleanup-throw") throw new InvalidOperationException("cleanup failure");
            }
        }
    }
}
