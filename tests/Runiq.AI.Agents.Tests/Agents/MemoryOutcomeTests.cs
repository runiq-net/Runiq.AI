using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Services;
using static Runiq.AI.Agents.Tests.Agents.MemoryContinuationTests;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class MemoryOutcomeTests
{
    [Fact]
    // Startup reconciliation uses the independent cleanup deadline and logs timeout without replacing the caller's cancellation.
    public async Task InitialAppendRecovery_TimeoutPreservesCallerCancellation()
    {
        using var caller = new CancellationTokenSource();
        var client = new Client();
        var fault = new Fault();
        var logger = new RecordingLogger();
        var attempts = new List<MemoryAppendRequest>();
        CancellationToken cleanupToken = default;
        fault.AfterAppend = async (request, token) =>
        {
            attempts.Add(request);
            if (attempts.Count == 1)
            {
                caller.Cancel();
                throw new OperationCanceledException(caller.Token);
            }
            cleanupToken = token;
            Assert.NotEqual(caller.Token, token);
            Assert.False(token.IsCancellationRequested);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        using var host = WithFault(Services(client), fault).AddSingleton<ILogger<AgentExecutionRuntime>>(logger).BuildServiceProvider();
        using var scope = host.CreateScope();
        var error = await Assert.ThrowsAsync<AgentRunCanceledException>(() => scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
            .ExecuteAsync("agent", new AgentQuery("first") { Memory = new("resource") }, caller.Token).WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.Equal(caller.Token, error.CancellationToken);
        Assert.Equal(AgentRunStatus.Cancelled, error.Run.Status);
        Assert.True(cleanupToken.IsCancellationRequested);
        Assert.Equal(2, attempts.Count);
        Assert.Same(attempts[0], attempts[1]);
        Assert.Contains(logger.Errors, e => e is OperationCanceledException cancellation && cancellation.CancellationToken == cleanupToken);
        Assert.Empty(client.Requests);
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = await Context(scope.ServiceProvider, error.Run.ThreadId!);
        Assert.Equal(MemoryTurnStatus.Running, Assert.Single(await store.ReadTurnsAsync(context)).Status);
        Assert.Single(await store.ReadMessagesAsync(context));
        Assert.Equal(1, (await store.ReadAsync(context)).Version);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    // A lost initial receipt must reach runtime cleanup, retain the exact request, and release the thread without changing the caller outcome.
    public async Task InitialAppendRecovery_FinalizesCommittedTurn(bool cancelAfterCommit, bool streaming)
    {
        using var caller = new CancellationTokenSource();
        var client = new Client();
        var fault = new Fault();
        var attempts = new List<(MemoryAppendRequest Request, CancellationToken Token)>();
        fault.AfterAppend = (request, token) =>
        {
            attempts.Add((request, token));
            if (cancelAfterCommit && attempts.Count == 1)
            {
                caller.Cancel();
                throw new OperationCanceledException(caller.Token);
            }
            if (!cancelAfterCommit && attempts.Count <= 2) throw new MemoryStoreException(MemoryStoreError.StorageFailure);
            return ValueTask.CompletedTask;
        };
        using var host = WithFault(Services(client), fault).BuildServiceProvider();
        using var scope = host.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var events = new List<AgentExecutionEvent>();
        async Task<AgentExecutionResult> Execute()
        {
            var query = new AgentQuery("first") { Memory = new("resource"), MemoryTurnId = "first-turn" };
            if (!streaming) return await runtime.ExecuteAsync("agent", query, caller.Token);
            var builder = new AgentExecutionResultBuilder();
            await foreach (var item in runtime.ExecuteStreamAsync("agent", query, cancellationToken: caller.Token))
            { events.Add(item); builder.Apply(item); }
            return builder.Build();
        }
        string thread;
        if (cancelAfterCommit)
        {
            var error = await Assert.ThrowsAsync<AgentRunCanceledException>(Execute);
            Assert.Equal(caller.Token, error.CancellationToken);
            Assert.Equal(AgentRunStatus.Cancelled, error.Run.Status);
            thread = error.Run.ThreadId!;
        }
        else
        {
            var result = await Execute();
            Assert.Equal("MemoryStorageFailure", result.ErrorCode);
            Assert.Equal(AgentRunStatus.Failed, result.Status);
            thread = result.ThreadId!;
        }
        Assert.DoesNotContain(events, e => e.Kind == AgentExecutionEventKind.Completed);
        Assert.Empty(client.Requests);
        Assert.Equal(cancelAfterCommit ? 3 : 4, attempts.Count);
        var original = attempts[0].Request;
        Assert.All(attempts.Take(attempts.Count - 1), a => Assert.Same(original, a.Request));
        var cleanup = attempts[^2].Token;
        Assert.True(cleanup.CanBeCanceled);
        Assert.False(cleanup.IsCancellationRequested);
        Assert.NotEqual(caller.Token, cleanup);
        Assert.Equal(cleanup, attempts[^1].Token);
        var context = await Context(scope.ServiceProvider, thread);
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        Assert.Equal(cancelAfterCommit ? MemoryTurnStatus.Cancelled : MemoryTurnStatus.Failed,
            Assert.Single(await store.ReadTurnsAsync(context)).Status);
        Assert.Equal(1, (await store.ReadAsync(context)).Version);
        Assert.Equal(original.Messages[0], Assert.Single(await store.ReadMessagesAsync(context)).Content);
        fault.AfterAppend = null;
        var next = await runtime.ExecuteAsync("agent", new AgentQuery("next") { Memory = new("resource", thread), MemoryTurnId = "next-turn" });
        Assert.True(next.IsSuccess, next.ErrorCode);
        Assert.Equal(3, (await store.ReadAsync(context)).Version);
        Assert.Equal(new[] { "instructions", "next" }, Assert.Single(client.Requests).Messages.Select(m => m.Content));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("dispose")]
    [InlineData("failure")]
    // Incomplete output remains diagnostic and is excluded from later model context for every interruption path.
    public async Task InterruptedOutput_IsNotReplayed(string mode)
    {
        using var cancellation = new CancellationTokenSource();
        var client = new Client();
        if (mode == "cancel") client.AfterUpdate = cancellation.Cancel;
        if (mode == "failure") client.AfterUpdate = () => throw new InvalidOperationException("private failure");
        using var host = Services(client).BuildServiceProvider();
        using var scope = host.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var events = new List<AgentExecutionEvent>();
        await using (var stream = runtime.ExecuteStreamAsync("agent", new AgentQuery("first") { Memory = new("resource") },
            cancellationToken: cancellation.Token).GetAsyncEnumerator())
        {
            Assert.True(await stream.MoveNextAsync()); events.Add(stream.Current);
            Assert.True(await stream.MoveNextAsync()); events.Add(stream.Current);
            if (mode == "cancel")
            {
                var error = await Assert.ThrowsAsync<AgentRunCanceledException>(() => stream.MoveNextAsync().AsTask());
                Assert.Equal(events[0].ThreadId, error.Run.ThreadId);
                Assert.Equal(cancellation.Token, error.CancellationToken);
            }
            else if (mode == "failure")
            {
                Assert.True(await stream.MoveNextAsync()); events.Add(stream.Current);
                Assert.Equal(AgentRunStatus.Failed, stream.Current.Status);
            }
        }
        Assert.DoesNotContain(events, e => e.Kind == AgentExecutionEventKind.Completed);
        var context = await Context(scope.ServiceProvider, events[0].ThreadId!);
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        Assert.Equal(mode == "failure" ? MemoryTurnStatus.Failed : MemoryTurnStatus.Cancelled,
            Assert.Single(await store.ReadTurnsAsync(context)).Status);
        Assert.Equal("answer", (await store.ReadMessagesAsync(context))[^1].Content.Message.Content);
        client.AfterUpdate = null;
        var next = await runtime.ExecuteAsync("agent", new AgentQuery("next") { Memory = new("resource", events[0].ThreadId) });
        Assert.True(next.IsSuccess, next.ErrorCode);
        Assert.Equal(new[] { "instructions", "next" }, client.Requests[^1].Messages.Select(m => m.Content));
    }

    [Theory]
    [InlineData("read")]
    [InlineData("start")]
    [InlineData("terminal")]
    // Read/write failures are safe visible failures and never publish successful completion before durable writes.
    public async Task PersistenceFailure_IsVisibleAndDoesNotComplete(string boundary)
    {
        var client = new Client();
        var fault = new Fault { Boundary = boundary };
        using var host = WithFault(Services(client), fault).BuildServiceProvider();
        using var scope = host.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent",
            new AgentQuery("question") { Memory = new("resource") });
        Assert.False(result.IsSuccess);
        Assert.Equal("MemoryStorageFailure", result.ErrorCode);
        Assert.DoesNotContain("private", result.ErrorMessage!);
        Assert.NotNull(result.ThreadId);
        Assert.Equal(boundary == "terminal" ? 1 : 0, client.Requests.Count);
        fault.Boundary = null;
        var turns = await scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>().ReadTurnsAsync(await Context(scope.ServiceProvider, result.ThreadId!));
        if (boundary == "terminal") Assert.Equal(MemoryTurnStatus.Running, Assert.Single(turns).Status);
        else Assert.Empty(turns);
    }

    [Fact]
    // A failed disconnected cleanup is logged while the original cancellation and unfinished durable status survive.
    public async Task CancelledFinalizationFailure_PreservesCancellationAndLogs()
    {
        using var cancellation = new CancellationTokenSource();
        var client = new Client { AfterUpdate = cancellation.Cancel };
        var fault = new Fault { Boundary = "terminal" };
        var logger = new RecordingLogger();
        using var host = WithFault(Services(client), fault).AddSingleton<ILogger<AgentExecutionRuntime>>(logger).BuildServiceProvider();
        using var scope = host.CreateScope();
        var error = await Assert.ThrowsAsync<AgentRunCanceledException>(() => scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
            .ExecuteAsync("agent", new AgentQuery("question") { Memory = new("resource") }, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Contains(logger.Errors, e => e is MemoryStoreException);
        fault.Boundary = null;
        Assert.Equal(MemoryTurnStatus.Running, Assert.Single(await scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>()
            .ReadTurnsAsync(await Context(scope.ServiceProvider, error.Run.ThreadId!))).Status);
    }

    [Fact]
    // Cancellation before enumeration creates no thread, invokes no model, and does not publish a fabricated terminal event.
    public async Task PreCancelled_DoesNoMemoryWrites()
    {
        var client = new Client();
        using var host = Services(client).BuildServiceProvider();
        using var scope = host.CreateScope();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var error = await Assert.ThrowsAsync<AgentRunCanceledException>(() => scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
            .ExecuteAsync("agent", new AgentQuery("question") { Memory = new("resource") }, cancellation.Token));
        Assert.Null(error.Run.ThreadId);
        Assert.Empty(client.Requests);
    }

    [Fact]
    // Cancelling a side-effecting tool leaves its call pending and excludes the entire turn from replay.
    public async Task ToolCancellation_LeavesIncompleteInteraction()
    {
        using var cancellation = new CancellationTokenSource();
        var client = new Client { Updates = _ => [new(Runiq.AI.Core.AI.Chat.ChatStreamingUpdateKind.ToolCallDelta,
            ToolCall: new("call", "echo", "{\"value\":\"cancel\"}"))] };
        var counter = new ToolCounter { Cancellation = cancellation };
        using var host = Services(client).AddSingleton(counter).BuildServiceProvider();
        using var scope = host.CreateScope();
        var error = await Assert.ThrowsAsync<AgentRunCanceledException>(() => scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
            .ExecuteAsync("agent", new AgentQuery("question") { Memory = new("resource") }, cancellation.Token));
        var context = await Context(scope.ServiceProvider, error.Run.ThreadId!);
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        Assert.Equal(1, counter.Calls);
        Assert.Equal(MemoryTurnStatus.Cancelled, Assert.Single(await store.ReadTurnsAsync(context)).Status);
        var transcript = await store.ReadMessagesAsync(context);
        Assert.Equal(2, transcript.Count);
        Assert.Single(transcript[1].Content.Message.ToolCalls!);
        Assert.DoesNotContain(transcript, m => m.Content.Message.Role == Runiq.AI.Core.AI.Chat.ChatRole.Tool);
    }

    internal static async Task<MemoryContext> Context(IServiceProvider services, string thread) =>
        (await services.GetRequiredService<MemoryAuthorizationService>().AuthorizeAsync(new("caller", "tenant"),
            new("resource", thread), "agent", new()))!;

    internal static IServiceCollection WithFault(IServiceCollection services, Fault fault)
    {
        var factory = services.Last(d => d.ServiceType == typeof(IMemoryConversationStore)).ImplementationFactory!;
        services.RemoveAll<IMemoryConversationStore>();
        services.AddScoped<IMemoryConversationStore>(p => new FaultStore((IMemoryConversationStore)factory(p), fault));
        return services;
    }

    internal sealed class Fault
    {
        internal string? Boundary;
        internal Func<MemoryAppendRequest, CancellationToken, ValueTask>? AfterAppend;
    }
    private sealed class FaultStore(IMemoryConversationStore inner, Fault fault) : IMemoryConversationStore
    {
        public ValueTask<MemoryConversation> CreateAsync(MemoryContext context, CancellationToken cancellationToken = default) => inner.CreateAsync(context, cancellationToken);
        public ValueTask<MemoryConversation> ReadAsync(MemoryContext context, CancellationToken cancellationToken = default) =>
            fault.Boundary == "read" ? throw Failure() : inner.ReadAsync(context, cancellationToken);
        public ValueTask<IReadOnlyList<MemoryConversation>> ListAsync(MemoryContext context, string? afterThreadId = null, int limit = 100, CancellationToken cancellationToken = default) => inner.ListAsync(context, afterThreadId, limit, cancellationToken);
        public ValueTask<IReadOnlyList<StoredMemoryMessage>> ReadMessagesAsync(MemoryContext context, long afterSequence = 0, int limit = 100, CancellationToken cancellationToken = default) => inner.ReadMessagesAsync(context, afterSequence, limit, cancellationToken);
        public ValueTask<IReadOnlyList<MemoryTurn>> ReadTurnsAsync(MemoryContext context, CancellationToken cancellationToken = default) => inner.ReadTurnsAsync(context, cancellationToken);
        public async ValueTask<MemoryAppendResult> AppendAsync(MemoryContext context, MemoryAppendRequest request, CancellationToken cancellationToken = default)
        {
            if (fault.Boundary == "start" && request.Turn?.Status == MemoryTurnStatus.Running ||
                fault.Boundary == "terminal" && request.Turn?.Status != MemoryTurnStatus.Running) throw Failure();
            var result = await inner.AppendAsync(context, request, cancellationToken);
            if (fault.AfterAppend is not null) await fault.AfterAppend(request, cancellationToken);
            return result;
        }
        private static MemoryStoreException Failure() => new(MemoryStoreError.StorageFailure, new Exception("private database diagnostic"));
    }

    private sealed class RecordingLogger : ILogger<AgentExecutionRuntime>
    {
        internal List<Exception> Errors = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (exception is not null) Errors.Add(exception); }
    }
}
