using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tests.TestDoubles;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Chat;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class AgentRuntimeDiagnosticsTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    // Verifies only caller-token cancellation wins; unrelated cancellation faults are sanitized and logged through either API.
    public async Task CancellationFault_UsesCallerTokenAndDisposesOnce(bool cleanup, bool callerCancelled, bool streaming)
    {
        using var cancellation = new CancellationTokenSource();
        var logger = new RecordingLogger();
        var executor = new CancellationFaultExecutor(cancellation, cleanup, callerCancelled);
        var services = CreateServices(logger);
        services.AddScoped(_ => new AgentExecutorResolver([executor]));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var events = new List<AgentExecutionEvent>();
        AgentExecutionResult? result = null;
        async Task Execute()
        {
            if (streaming)
            {
                var builder = new AgentExecutionResultBuilder();
                await foreach (var item in runtime.ExecuteStreamAsync("agent", "question", cancellationToken: cancellation.Token))
                {
                    events.Add(item);
                    builder.Apply(item);
                }
                result = builder.Build();
            }
            else result = await runtime.ExecuteAsync("agent", "question", cancellation.Token);
        }
        if (callerCancelled)
        {
            var error = await Assert.ThrowsAsync<AgentRunCanceledException>(Execute);
            Assert.Same(executor.Run, error.Run);
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.Equal(AgentRunStatus.Cancelled, error.Run.Status);
            Assert.Null(result);
            Assert.All(events, item => Assert.Equal(AgentRunStatus.Running, item.Status));
        }
        else
        {
            await Execute();
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Equal(AgentRunStatus.Failed, result.Status);
            Assert.Equal("AgentExecutionFailed", result.ErrorCode);
            Assert.Equal(cleanup ? "Agent executor cleanup failed." : "Agent execution failed.", result.ErrorMessage);
            Assert.Equal(executor.Run!.RunId, result.RunId);
            if (streaming) Assert.Single(events, item => item.Status != AgentRunStatus.Running);
        }
        Assert.Equal(2, executor.Reads);
        Assert.Equal(1, executor.Disposals);
        Assert.NotNull(executor.Run!.EndedAt);
        Assert.All(events, item => Assert.Equal(executor.Run.RunId, item.RunId));
        if (!callerCancelled || cleanup)
            AssertLog(Assert.Single(logger.Entries), executor.Failure, executor.Run.RunId,
                callerCancelled ? LogLevel.Warning : LogLevel.Error);
        else Assert.Empty(logger.Entries);
    }

    [Fact]
    // Verifies cancellation during terminal cleanup prevents completion publication and disposes the executor once.
    public async Task TerminalCleanup_CancellationWinsBeforePublication()
    {
        using var cancellation = new CancellationTokenSource();
        var executor = new CancellingCleanupExecutor(cancellation);
        var services = CreateServices(new RecordingLogger());
        services.AddScoped(_ => new AgentExecutorResolver([executor]));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var events = new List<AgentExecutionEvent>();
        var exception = await Assert.ThrowsAsync<AgentRunCanceledException>(async () =>
        {
            await foreach (var item in scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
                .ExecuteStreamAsync("agent", "question", cancellationToken: cancellation.Token)) events.Add(item);
        });
        Assert.Equal(AgentRunStatus.Cancelled, exception.Run.Status);
        Assert.Single(events);
        Assert.All(events, item => Assert.Equal(AgentRunStatus.Running, item.Status));
        Assert.Equal(1, executor.Disposals);
    }

    [Fact]
    // Verifies the hosting graph resolves scoped executors and logs original model faults without exposing them to callers.
    public async Task Hosting_ResolvesScopedModelExecutorAndLogsExecutionFailure()
    {
        var logger = new RecordingLogger();
        var failure = new InvalidOperationException("Internal provider diagnostic");
        var services = CreateServices(logger);
        services.AddScoped<IChatClientResolver>(_ => new ThrowingChatResolver(failure));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        Assert.Same(first.ServiceProvider.GetServices<IAgentExecutor>().Single(),
            first.ServiceProvider.GetServices<IAgentExecutor>().Single());
        Assert.NotSame(first.ServiceProvider.GetServices<IAgentExecutor>().Single(),
            second.ServiceProvider.GetServices<IAgentExecutor>().Single());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<AgentExecutorResolver>(),
            second.ServiceProvider.GetRequiredService<AgentExecutorResolver>());

        var result = await first.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent", "question");

        Assert.Equal("AgentExecutionFailed", result.ErrorCode);
        Assert.Equal("Agent execution failed.", result.ErrorMessage);
        AssertLog(Assert.Single(logger.Entries), failure, result.RunId!, LogLevel.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Verifies runtime uses the DI-supplied resolver and preserves both execution and disposal exceptions in logs.
    public async Task Hosting_InjectedExecutorLogsCleanupAndOriginalExecutionFailure(bool failExecution)
    {
        var logger = new RecordingLogger();
        var executor = new FaultingExecutor(failExecution);
        var services = CreateServices(logger);
        services.AddScoped(_ => new AgentExecutorResolver([executor]));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent", "question");

        Assert.Equal("AgentExecutionFailed", result.ErrorCode);
        Assert.Equal("Agent executor cleanup failed.", result.ErrorMessage);
        Assert.Equal(AgentRunStatus.Failed, executor.Run!.Status);
        Assert.Equal(failExecution ? 2 : 1, logger.Entries.Count);
        if (failExecution) AssertLog(logger.Entries[0], executor.ExecutionFailure, result.RunId!, LogLevel.Error);
        AssertLog(logger.Entries[^1], executor.CleanupFailure, result.RunId!, LogLevel.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Verifies cancellation and consumer abandonment log cleanup faults without replacing the Cancelled state or exception.
    public async Task CancelledRun_LogsCleanupFailureAndRetainsCancellation(bool cancelToken)
    {
        using var cancellation = new CancellationTokenSource();
        var logger = new RecordingLogger();
        var executor = new FaultingExecutor(false, partial: true);
        var services = CreateServices(logger);
        services.AddScoped(_ => new AgentExecutorResolver([executor]));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        await using var stream = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
            .ExecuteStreamAsync("agent", "question", cancellationToken: cancellation.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        var runId = stream.Current.RunId!;

        if (cancelToken)
        {
            cancellation.Cancel();
            var exception = await Assert.ThrowsAsync<AgentRunCanceledException>(async () => await stream.MoveNextAsync());
            Assert.Same(executor.Run, exception.Run);
            Assert.Equal(cancellation.Token, exception.CancellationToken);
        }
        else
        {
            await stream.DisposeAsync();
        }

        Assert.Equal(AgentRunStatus.Cancelled, executor.Run!.Status);
        AssertLog(Assert.Single(logger.Entries), executor.CleanupFailure, runId, LogLevel.Warning);
    }

    private static ServiceCollection CreateServices(RecordingLogger logger)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Agent("agent", "Agent", "instructions", "openai/model", "key"));
        services.AddSingleton<ILogger<AgentExecutionRuntime>>(logger);
        services.AddScoped<IChatClientResolver, TestChatClientResolver>();
        services.AddRuniqAgentServer();
        return services;
    }

    private static void AssertLog(LogEntry entry, Exception failure, string runId, LogLevel level)
    {
        Assert.Same(failure, entry.Exception);
        Assert.False(string.IsNullOrWhiteSpace(entry.Exception!.StackTrace));
        Assert.Equal(level, entry.Level);
        Assert.Equal(runId, entry.Properties["RunId"]);
        Assert.Equal("agent", entry.Properties["AgentId"]);
        Assert.DoesNotContain("question", entry.Properties.Values);
        Assert.DoesNotContain("key", entry.Properties.Values);
    }

    private sealed class CancellationFaultExecutor(CancellationTokenSource caller, bool cleanup, bool cancelCaller) :
        IAgentExecutor, IAsyncEnumerable<AgentExecutionEvent>, IAsyncEnumerator<AgentExecutionEvent>
    {
        internal int Reads, Disposals;
        internal AgentRunContext? Run;
        internal OperationCanceledException Failure { get; } = new("Private executor diagnostic", new CancellationToken(true));
        public Runiq.AI.Agents.Configuration.AgentExecutorKind Kind => Runiq.AI.Agents.Configuration.AgentExecutorKind.Model;
        public AgentExecutionEvent Current => Reads == 1 ? AgentExecutionEvent.AssistantDelta("partial") : AgentExecutionEvent.Completed();
        public IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request, AgentRunContext run,
            AgentToolInvoker toolInvoker, CancellationToken cancellationToken)
        {
            Assert.Equal(caller.Token, cancellationToken);
            Run = run;
            return this;
        }
        public IAsyncEnumerator<AgentExecutionEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            Assert.Equal(caller.Token, cancellationToken);
            return this;
        }
        public ValueTask<bool> MoveNextAsync()
        {
            Reads++;
            if (Reads == 2 && !cleanup) Fail();
            return ValueTask.FromResult(Reads <= 2);
        }
        public ValueTask DisposeAsync()
        {
            Disposals++;
            Assert.Equal(cancelCaller && !cleanup ? AgentRunStatus.Cancelled : AgentRunStatus.Running, Run!.Status);
            if (cleanup) Fail();
            return ValueTask.CompletedTask;
        }
        private void Fail()
        {
            if (cancelCaller) caller.Cancel();
            throw Failure;
        }
    }

    private sealed class ThrowingChatResolver(Exception failure) : IChatClientResolver
    {
        public IChatClient Resolve(ChatRequest request) => throw failure;
    }

    private sealed class CancellingCleanupExecutor(CancellationTokenSource cancellation) : IAgentExecutor,
        IAsyncEnumerable<AgentExecutionEvent>, IAsyncEnumerator<AgentExecutionEvent>
    {
        private int step;
        internal int Disposals;
        public Runiq.AI.Agents.Configuration.AgentExecutorKind Kind => Runiq.AI.Agents.Configuration.AgentExecutorKind.Model;
        public AgentExecutionEvent Current => step == 1 ? AgentExecutionEvent.AssistantDelta("answer") : AgentExecutionEvent.Completed();
        public IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request, AgentRunContext run,
            AgentToolInvoker toolInvoker, CancellationToken cancellationToken) => this;
        public IAsyncEnumerator<AgentExecutionEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(++step <= 2);
        public ValueTask DisposeAsync()
        {
            Disposals++;
            cancellation.Cancel();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FaultingExecutor(bool failExecution, bool partial = false) : IAgentExecutor,
        IAsyncEnumerable<AgentExecutionEvent>, IAsyncEnumerator<AgentExecutionEvent>
    {
        public Runiq.AI.Agents.Configuration.AgentExecutorKind Kind => Runiq.AI.Agents.Configuration.AgentExecutorKind.Model;

        internal Exception ExecutionFailure { get; } = new InvalidOperationException("Original execution diagnostic");
        internal Exception CleanupFailure { get; } = new InvalidOperationException("Original cleanup diagnostic");
        internal AgentRunContext? Run { get; private set; }
        public AgentExecutionEvent Current => partial
            ? AgentExecutionEvent.AssistantDelta("answer") : AgentExecutionEvent.Completed();

        public IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request,
            AgentRunContext run, AgentToolInvoker toolInvoker, CancellationToken cancellationToken)
        {
            Run = run;
            return this;
        }

        public IAsyncEnumerator<AgentExecutionEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        public ValueTask<bool> MoveNextAsync() => failExecution ? throw ExecutionFailure : ValueTask.FromResult(true);
        public ValueTask DisposeAsync() => throw CleanupFailure;
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, Dictionary<string, object?> Properties);

    private sealed class RecordingLogger : ILogger<AgentExecutionRuntime>
    {
        internal List<LogEntry> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new(logLevel, exception, ((IEnumerable<KeyValuePair<string, object?>>)state!)
                .ToDictionary(pair => pair.Key, pair => pair.Value)));
    }
}
