using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.DependencyInjection;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class MemoryPreflightDiagnosticsTests
{
    [Theory]
    [InlineData("ServiceResolution", false)]
    [InlineData("ServiceResolution", true)]
    [InlineData("IdentityResolution", false)]
    [InlineData("IdentityResolution", true)]
    [InlineData("OwnershipLookup", false)]
    [InlineData("OwnershipLookup", true)]
    [InlineData("ResourcePolicy", false)]
    [InlineData("ResourcePolicy", true)]
    // Technical failures retain their original exception and run correlation without exposing diagnostics to either client API.
    public async Task UnexpectedFailure_LogsOriginalExceptionAndPreservesSafeResponse(string boundary, bool streaming)
    {
        var failure = new InvalidOperationException("Internal adapter diagnostic");
        var probe = new PreflightProbe(boundary, failure);
        var logger = new RecordingLogger();
        using var provider = CreateServices(probe, logger).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var result = await Execute(runtime, streaming);

        Assert.Equal(boundary == "ServiceResolution" ? "MemoryServicesMissing" : "MemoryPreflightFailed", result.ErrorCode);
        Assert.Equal(boundary == "ServiceResolution" ? "Required Memory services are unavailable." : "Memory preflight failed.", result.ErrorMessage);
        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.Null(result.Message);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(failure, entry.Exception);
        Assert.False(string.IsNullOrWhiteSpace(entry.Exception!.StackTrace));
        Assert.Equal(result.RunId, entry.Properties["RunId"]);
        Assert.Equal("agent", entry.Properties["AgentId"]);
        Assert.Equal(boundary is "OwnershipLookup" or "ResourcePolicy" ? "Authorization" : boundary,
            entry.Properties["MemoryPreflightStage"]);
        Assert.Equal(new[] { "AgentId", "MemoryPreflightStage", "RunId", "{OriginalFormat}" }, entry.Properties.Keys.Order(StringComparer.Ordinal));
        foreach (var sensitive in new[] { "private-message", "private-caller", "private-tenant", "private-resource", "private-thread", "private-key" })
        {
            Assert.DoesNotContain(sensitive, entry.Message);
            Assert.DoesNotContain(sensitive, entry.Properties.Values);
        }
        Assert.DoesNotContain(failure.Message, result.ErrorMessage);
        Assert.Equal(0, probe.ModelCalls);
    }

    [Theory]
    [InlineData("ServiceResolution", false)]
    [InlineData("ServiceResolution", true)]
    [InlineData("IdentityResolution", false)]
    [InlineData("IdentityResolution", true)]
    [InlineData("OwnershipLookup", false)]
    [InlineData("OwnershipLookup", true)]
    [InlineData("ResourcePolicy", false)]
    [InlineData("ResourcePolicy", true)]
    // Caller cancellation wins over both cancellation exceptions and coincident adapter faults without error logging.
    public async Task CallerCancellation_DoesNotLogUnexpectedFailure(string boundary, bool cancellationException)
    {
        using var cancellation = new CancellationTokenSource();
        Exception failure = cancellationException ? new OperationCanceledException(cancellation.Token)
            : new InvalidOperationException("Concurrent adapter failure");
        var probe = new PreflightProbe(boundary, failure, cancellation);
        var logger = new RecordingLogger();
        using var provider = CreateServices(probe, logger).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();

        var error = await Assert.ThrowsAsync<AgentRunCanceledException>(() => Execute(runtime, streaming: true, cancellation.Token));

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(AgentRunStatus.Cancelled, error.Run.Status);
        Assert.NotNull(error.Run.EndedAt);
        Assert.Empty(logger.Entries);
        Assert.Equal(0, probe.ModelCalls);
    }

    [Theory]
    [InlineData("ServiceResolution")]
    [InlineData("IdentityResolution")]
    [InlineData("OwnershipLookup")]
    // A dependency's independent cancellation is an unexpected technical failure when the caller has not cancelled.
    public async Task UnrelatedCancellation_IsLogged(string boundary)
    {
        var failure = new OperationCanceledException("Adapter timed out", new CancellationToken(true));
        var probe = new PreflightProbe(boundary, failure);
        var logger = new RecordingLogger();
        using var provider = CreateServices(probe, logger).BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await Execute(scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>(), streaming: false);

        Assert.Equal(boundary == "ServiceResolution" ? "MemoryServicesMissing" : "MemoryPreflightFailed", result.ErrorCode);
        Assert.Same(failure, Assert.Single(logger.Entries).Exception);
        Assert.Equal(0, probe.ModelCalls);
    }

    [Theory]
    [InlineData("denied")]
    [InlineData("unknown")]
    [InlineData("anonymous")]
    // Expected access and identity denials remain quiet while retaining their established safe client messages.
    public async Task ExpectedDenial_DoesNotLogError(string outcome)
    {
        var probe = new PreflightProbe(outcome, null);
        var logger = new RecordingLogger();
        using var provider = CreateServices(probe, logger).BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await Execute(scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>(), streaming: false);

        Assert.Equal(outcome == "anonymous" ? "MemoryIdentityRequired" : "MemoryAccessDenied", result.ErrorCode);
        Assert.Equal(outcome == "anonymous" ? "A verified Memory identity is required." : "Memory access was denied.", result.ErrorMessage);
        Assert.Empty(logger.Entries);
        Assert.Equal(0, probe.ModelCalls);
    }

    private static IServiceCollection CreateServices(PreflightProbe probe, RecordingLogger logger)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<AgentExecutionRuntime>>(logger);
        services.AddRuniqServer(options => options.AddAgent(new Agent("agent", "Agent", "instructions", "openai/model", "private-key").UseMemory()));
        services.AddRuniqMemory();
        services.AddScoped<IMemoryIdentityResolver>(_ =>
        {
            probe.Visit("ServiceResolution");
            return probe;
        });
        services.AddScoped<IMemoryOwnershipLookup>(_ => probe);
        services.AddScoped<IMemoryAccessPolicy>(_ => probe);
        services.AddScoped<IChatClientResolver>(_ => probe);
        return services;
    }

    private static async Task<AgentExecutionResult> Execute(AgentExecutionRuntime runtime, bool streaming,
        CancellationToken cancellationToken = default)
    {
        var query = new AgentQuery("private-message") { Memory = new("private-resource", "private-thread") };
        if (!streaming) return await runtime.ExecuteAsync("agent", query, cancellationToken);
        var builder = new AgentExecutionResultBuilder();
        var terminalEvents = 0;
        await foreach (var item in runtime.ExecuteStreamAsync("agent", query, cancellationToken: cancellationToken))
        {
            builder.Apply(item);
            if (item.Status != AgentRunStatus.Running) terminalEvents++;
        }
        Assert.Equal(1, terminalEvents);
        return builder.Build();
    }

    private sealed class PreflightProbe(string boundary, Exception? failure, CancellationTokenSource? cancellation = null)
        : IMemoryIdentityResolver, IMemoryOwnershipLookup, IMemoryAccessPolicy, IChatClientResolver
    {
        internal int ModelCalls;
        internal void Visit(string stage)
        {
            if (boundary != stage) return;
            cancellation?.Cancel();
            if (failure is not null) throw failure;
        }
        public ValueTask<MemoryIdentity?> ResolveAsync(CancellationToken cancellationToken)
        {
            Visit("IdentityResolution");
            return ValueTask.FromResult<MemoryIdentity?>(boundary == "anonymous" ? null : new("private-caller", "private-tenant"));
        }
        public ValueTask<MemoryThreadOwnership?> FindAsync(string boundaryId, string threadId, CancellationToken cancellationToken)
        {
            Visit("OwnershipLookup");
            return ValueTask.FromResult<MemoryThreadOwnership?>(boundary == "unknown" ? null : new(threadId, new(boundaryId, "private-resource", "agent")));
        }
        public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken)
        {
            Visit("ResourcePolicy");
            return ValueTask.FromResult(boundary != "denied");
        }
        public IChatClient Resolve(ChatRequest request)
        {
            ModelCalls++;
            throw new InvalidOperationException("Model resolution must not occur after failed Memory preflight.");
        }
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message, Dictionary<string, object?> Properties);

    private sealed class RecordingLogger : ILogger<AgentExecutionRuntime>
    {
        internal List<LogEntry> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new(logLevel, exception, formatter(state, exception),
                ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(pair => pair.Key, pair => pair.Value)));
    }
}
