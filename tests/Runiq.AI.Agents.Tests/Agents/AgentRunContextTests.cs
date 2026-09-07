using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class AgentRunContextTests
{
    [Theory]
    [InlineData("success", AgentRunStatus.Completed)]
    [InlineData("failure", AgentRunStatus.Failed)]
    [InlineData("cancel", AgentRunStatus.Cancelled)]
    [InlineData("abandon", AgentRunStatus.Cancelled)]
    // Verifies laziness, lossless request forwarding, timestamp ownership and fresh contexts on repeated enumeration.
    public async Task RuntimeContext_TracksEachEnumeration(string mode, AgentRunStatus expected)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var executor = new ContextExecutor();
        var agent = new Agent("agent", "Agent", "instructions").UseCodex();
        var query = new AgentQuery(mode) { IndexName = "override-index" };
        var runtime = CreateRuntime(agent, executor, services);
        var stream = runtime.ExecuteStreamAsync(agent.Id, query, cancellationToken: cancellation.Token);
        await using var enumerator = stream.GetAsyncEnumerator();
        Assert.Empty(executor.Calls);
        var before = DateTimeOffset.UtcNow;
        Assert.True(await enumerator.MoveNextAsync());
        var call = Assert.Single(executor.Calls);
        Assert.Same(agent, call.Request.Agent);
        Assert.Same(query, call.Request.Query);
        Assert.Equal("override-index", call.Request.Query.IndexName);
        Assert.Equal(enumerator.Current.RunId, call.Run.RunId);
        Assert.NotEqual(query.Message, call.Run.RunId);
        Assert.Null(call.Run.ProviderSessionId);
        Assert.Equal(agent.Id, call.Run.AgentId);
        Assert.InRange(call.Run.StartedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(AgentRunStatus.Running, call.Run.Status);
        Assert.Null(call.Run.EndedAt);
        var builder = new AgentExecutionResultBuilder();
        builder.Apply(enumerator.Current);
        if (mode == "cancel")
        {
            cancellation.Cancel();
            var error = await Assert.ThrowsAsync<AgentRunCanceledException>(async () => await enumerator.MoveNextAsync());
            Assert.Same(call.Run, error.Run);
        }
        else if (mode == "abandon") await enumerator.DisposeAsync();
        else
        {
            while (await enumerator.MoveNextAsync()) builder.Apply(enumerator.Current);
            var result = builder.Build();
            Assert.Equal(call.Run.RunId, result.RunId);
            Assert.Equal(expected, result.Status);
        }
        Assert.Equal(expected, call.Run.Status);
        var endedAt = Assert.IsType<DateTimeOffset>(call.Run.EndedAt);
        Assert.InRange(endedAt, call.Run.StartedAt, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, endedAt.Offset);
        await enumerator.DisposeAsync();
        Assert.Equal(endedAt, call.Run.EndedAt);
        if (mode != "cancel")
        {
            await foreach (var item in stream) Assert.NotEqual(call.Run.RunId, item.RunId);
            Assert.Equal(2, executor.Calls.Count);
            Assert.Equal(endedAt, call.Run.EndedAt);
        }
    }

    [Fact]
    // Verifies overlapping enumerations of the same stream have isolated contexts when one is cancelled and the other completes.
    public async Task ConcurrentEnumerations_IsolateStatusAndTimestamps()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var executor = new ContextExecutor();
        var agent = new Agent("agent", "Agent", "instructions").UseCodex();
        var stream = CreateRuntime(agent, executor, services).ExecuteStreamAsync(agent.Id, new AgentQuery("same input"));
        await using var first = stream.GetAsyncEnumerator(cancellation.Token);
        await using var second = stream.GetAsyncEnumerator();
        Assert.All(await Task.WhenAll(first.MoveNextAsync().AsTask(), second.MoveNextAsync().AsTask()), Assert.True);
        var one = executor.Calls.Single(call => call.Run.RunId == first.Current.RunId).Run;
        var two = executor.Calls.Single(call => call.Run.RunId == second.Current.RunId).Run;
        Assert.NotSame(one, two);
        Assert.NotEqual(one.RunId, two.RunId);
        cancellation.Cancel();
        await Assert.ThrowsAsync<AgentRunCanceledException>(async () => await first.MoveNextAsync());
        Assert.Equal(AgentRunStatus.Cancelled, one.Status);
        Assert.NotNull(one.EndedAt);
        Assert.Equal(AgentRunStatus.Running, two.Status);
        Assert.Null(two.EndedAt);
        var builder = new AgentExecutionResultBuilder();
        builder.Apply(second.Current);
        while (await second.MoveNextAsync()) builder.Apply(second.Current);
        Assert.Equal(two.RunId, builder.Build().RunId);
        Assert.Equal(AgentRunStatus.Completed, two.Status);
        Assert.NotNull(two.EndedAt);
        Assert.Equal(AgentRunStatus.Cancelled, one.Status);
    }

    [Fact]
    // Verifies competing terminal transitions publish one immutable timestamp and never expose terminal status without an end time.
    public async Task TerminalRace_PublishesTimestampWithStatus()
    {
        var run = new AgentRunContext("agent");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new[] { AgentRunStatus.Completed, AgentRunStatus.Failed, AgentRunStatus.Cancelled }.Select(async status =>
        {
            await gate.Task;
            run.Finish(status);
            Assert.NotEqual(AgentRunStatus.Running, run.Status);
            return Assert.IsType<DateTimeOffset>(run.EndedAt);
        }).ToArray();
        gate.SetResult();
        var ends = await Task.WhenAll(tasks);
        Assert.All(ends, end => Assert.Equal(run.EndedAt, end));
    }

    private static AgentExecutionRuntime CreateRuntime(Agent agent, IAgentExecutor executor, IServiceProvider services) =>
        new([agent], new AgentExecutorResolver([executor]), new AgentToolInvoker(services), NullLogger<AgentExecutionRuntime>.Instance);

    private sealed class ContextExecutor : IAgentExecutor
    {
        internal ConcurrentBag<(AgentExecutionRequest Request, AgentRunContext Run)> Calls { get; } = [];
        public AgentExecutorKind Kind => AgentExecutorKind.Codex;
        public async IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request, AgentRunContext run,
            AgentToolInvoker toolInvoker, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Calls.Add((request, run));
            yield return AgentExecutionEvent.AssistantDelta("answer");
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return request.Query.Message == "failure" ? AgentExecutionEvent.Failed("controlled failure") : AgentExecutionEvent.Completed();
        }
    }
}
