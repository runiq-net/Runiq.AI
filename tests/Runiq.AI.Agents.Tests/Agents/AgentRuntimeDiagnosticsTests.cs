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
        Assert.Same(first.ServiceProvider.GetRequiredService<ModelAgentExecutor>(),
            first.ServiceProvider.GetRequiredService<ModelAgentExecutor>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<ModelAgentExecutor>(),
            second.ServiceProvider.GetRequiredService<ModelAgentExecutor>());
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
        services.AddScoped(_ => new AgentExecutorResolver(executor));
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
        services.AddScoped(_ => new AgentExecutorResolver(executor));
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

    private sealed class ThrowingChatResolver(Exception failure) : IChatClientResolver
    {
        public IChatClient Resolve(ChatRequest request) => throw failure;
    }

    private sealed class FaultingExecutor(bool failExecution, bool partial = false) : IAgentExecutor,
        IAsyncEnumerable<AgentExecutionEvent>, IAsyncEnumerator<AgentExecutionEvent>
    {
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
