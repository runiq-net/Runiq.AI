using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.DependencyInjection;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Services;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class MemoryRuntimeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // All registered and direct entry points share a preflight that cannot dispatch denied work.
    public async Task EntryPoints_EnforceAuthorization(bool allowed)
    {
        var probe = new Probe { Allowed = allowed };
        var agent = CreateAgent();
        using var provider = CreateServices(agent, probe).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var query = new AgentQuery("question") { Memory = new("project", allowed ? null : "thread") };
        var results = new[] { await runtime.ExecuteAsync(agent, query), await runtime.ExecuteAsync(agent.Id, query) };
        var events = await Collect(runtime.ExecuteStreamAsync(agent.Id, query));
        Assert.All(results, result => Assert.Equal(allowed, result.IsSuccess));
        Assert.Single(events, x => x.Status != AgentRunStatus.Running);
        Assert.Equal(allowed ? AgentRunStatus.Completed : AgentRunStatus.Failed, events[^1].Status);
        Assert.Equal(3, results.Select(x => x.RunId).Append(events[^1].RunId).Distinct().Count());
        Assert.Equal(allowed ? 3 : 0, probe.Requests.Count);
        Assert.Equal(3, probe.IdentityCalls);
        if (!allowed) Assert.All(results, result => Assert.Equal("MemoryAccessDenied", result.ErrorCode));
        else Assert.All(probe.Requests, request =>
        {
            Assert.Equal("caller", request.Memory!.Identity.CallerId);
            Assert.False(string.IsNullOrWhiteSpace(request.Memory.Ownership.ThreadId));
            Assert.Same(query, request.Query);
        });

        // String-only overloads still run preflight but cannot invent a resource owner.
        Assert.Equal("MemoryReferenceRequired", (await runtime.ExecuteAsync(agent, "question")).ErrorCode);
        Assert.Equal("MemoryReferenceRequired", (await runtime.ExecuteAsync(agent.Id, "question")).ErrorCode);
        Assert.Equal("MemoryReferenceRequired", (await Collect(runtime.ExecuteStreamAsync(agent.Id, "question")))[^1].ErrorCode);
    }

    [Fact]
    // Disabled execution must not even construct optional Memory services registered by a host.
    public async Task Disabled_DoesNotResolveMemoryServices()
    {
        var agent = new Agent("agent", "Agent", "instructions", "openai/model");
        var probe = new Probe();
        var services = CreateServices(agent, probe);
        services.AddScoped<IMemoryIdentityResolver>(_ => throw new Exception("must not resolve identity"));
        services.AddScoped<MemoryAuthorizationService>(_ => throw new Exception("must not resolve authorization"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        Assert.True((await runtime.ExecuteAsync(agent.Id, "question")).IsSuccess);
        Assert.Null(Assert.Single(probe.Requests).Memory);
        Assert.Equal(0, probe.IdentityCalls);
    }

    [Theory]
    [InlineData("store", "MemoryServicesMissing")]
    [InlineData("identity", "MemoryServicesMissing")]
    [InlineData("authorization", "MemoryServicesMissing")]
    [InlineData("ownership", "MemoryServicesMissing")]
    [InlineData("policy", "MemoryServicesMissing")]
    // Incomplete foundation registration cannot silently fall back to a stateless model invocation.
    public async Task MissingServices_FailExplicitly(string missing, string expected)
    {
        var agent = CreateAgent();
        var probe = new Probe();
        var services = CreateServices(agent, probe);
        if (missing == "store") services.RemoveAll<IMemoryConversationStore>();
        if (missing == "identity") services.RemoveAll<IMemoryIdentityResolver>();
        if (missing == "authorization") services.RemoveAll<MemoryAuthorizationService>();
        if (missing == "ownership") services.RemoveAll<IMemoryOwnershipLookup>();
        if (missing == "policy") services.RemoveAll<IMemoryAccessPolicy>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync(agent, Query());
        Assert.Equal(expected, result.ErrorCode);
        Assert.Empty(probe.Requests);
    }

    [Theory]
    [InlineData("anonymous", "MemoryIdentityRequired")]
    [InlineData("identity", "MemoryPreflightFailed")]
    [InlineData("lookup", "MemoryPreflightFailed")]
    [InlineData("unknown", "MemoryAccessDenied")]
    // Failures expose stable safe codes without leaking host exception text or another conversation.
    public async Task Failures_AreSafeAndStopDispatch(string failure, string expected)
    {
        var probe = new Probe { Failure = failure };
        using var provider = CreateServices(CreateAgent(), probe).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent", Query());
        Assert.Equal(expected, result.ErrorCode);
        Assert.DoesNotContain("secret", result.ErrorMessage!);
        Assert.Empty(probe.Requests);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("lookup")]
    // Cancellation during preflight retains runtime cancellation semantics and never starts an executor.
    public async Task PreflightCancellation_PreservesRunLifecycle(string boundary)
    {
        using var cancellation = new CancellationTokenSource();
        var probe = new Probe { CancelAt = boundary, Cancellation = cancellation };
        using var provider = CreateServices(CreateAgent(), probe).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        var error = await Assert.ThrowsAsync<AgentRunCanceledException>(() => runtime.ExecuteAsync("agent", Query(), cancellation.Token));
        Assert.Equal(AgentRunStatus.Cancelled, error.Run.Status);
        Assert.NotNull(error.Run.EndedAt);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Empty(probe.Requests);
    }

    [Fact]
    // Creating a stream is lazy; disposing after a delta cancels the run and disposes its executor once.
    public async Task Stream_IsLazyAndDisposesUnfinishedRun()
    {
        var probe = new Probe();
        using var provider = CreateServices(CreateAgent(), probe).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var stream = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteStreamAsync("agent", new AgentQuery("question") { Memory = new("project") });
        Assert.Equal(0, probe.IdentityCalls);
        var enumerator = stream.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(1, probe.IdentityCalls);
        Assert.Equal(AgentExecutionEventKind.ConversationStarted, enumerator.Current.Kind);
        Assert.True(await enumerator.MoveNextAsync());
        await enumerator.DisposeAsync();
        Assert.Equal(1, probe.Disposals);
        Assert.Equal(AgentRunStatus.Cancelled, Assert.Single(probe.Runs).Status);
    }

    [Fact]
    // Scoped background host identities remain isolated while sharing one singleton agent definition.
    public async Task ConcurrentDirectHosts_KeepIdentityPerScope()
    {
        var agent = CreateAgent();
        var probe = new Probe();
        var services = CreateServices(agent, probe);
        services.AddScoped<HostIdentity>();
        services.AddScoped<IMemoryIdentityResolver>(p => p.GetRequiredService<HostIdentity>());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async index =>
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<HostIdentity>().Identity = new($"caller-{index}", $"tenant-{index}");
            await Task.Yield();
            return await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync(agent,
                new AgentQuery("question") { Memory = new("project") });
        }));
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(12, probe.Requests.Select(x => x.Memory!.Identity.CallerId).Distinct().Count());
        Assert.All(probe.Requests, x => Assert.Equal(x.Memory!.Identity.BoundaryId, x.Memory.AccessScope.BoundaryId));
        Assert.Equal(12, results.Select(x => x.RunId).Distinct().Count());
        Assert.Null(agent.Memory!.SharingGroup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Existing manual constructors share the preflight and cannot bypass enabled Memory requirements.
    public async Task ManualConstruction_CannotBypassPreflight(bool clients)
    {
        var agent = CreateAgent();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var forbidden = new ForbiddenClient();
        var invoker = new AgentToolInvoker(provider);
        var runtime = clients ? new AgentExecutionRuntime([agent], forbidden, forbidden, invoker)
            : new AgentExecutionRuntime([agent], forbidden, invoker);
        Assert.Equal("MemoryServicesMissing", (await runtime.ExecuteAsync(agent, Query())).ErrorCode);
    }

    [Fact]
    // Built-in model execution must stop before provider resolution when ownership is denied.
    public async Task BuiltInModel_DenialPrecedesProviderResolution()
    {
        var probe = new Probe { Allowed = false };
        var agent = CreateAgent().UseRag(x => x.IndexName = "documents");
        var services = CreateServices(agent, probe, customExecutor: false);
        services.AddScoped<IChatClientResolver, ForbiddenClient>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Equal("MemoryAccessDenied", (await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>()
            .ExecuteAsync(agent, Query())).ErrorCode);
    }

    [Fact]
    // An authorized new conversation reaches the built-in model and returns a durable thread without a provider session.
    public async Task BuiltInModel_AllowedExecutionPreservesOutput()
    {
        var probe = new Probe();
        var services = CreateServices(CreateAgent(), probe, customExecutor: false);
        var client = new AllowedClient();
        services.AddScoped<IChatClientResolver>(_ => client);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent", new AgentQuery("question") { Memory = new("project") });
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("answer", result.Message);
        Assert.Null(result.ProviderSessionId);
        Assert.Equal(1, client.Calls);
        Assert.Equal(1, probe.IdentityCalls);
    }

    [Theory]
    [InlineData(false, false, "MemoryExecutorNotSupported")]
    [InlineData(true, true, "MemoryContinuationConflict")]
    // Unsupported custom executors and mixed continuation fail before identity or downstream execution.
    public async Task CompatibilityFailure_PrecedesIdentityResolution(bool supportsMemory, bool mixed, string expected)
    {
        var probe = new Probe { SupportsMemoryFoundation = supportsMemory };
        using var provider = CreateServices(CreateAgent(), probe).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var query = new AgentQuery("question") { Memory = new("project", "thread"), ProviderSessionId = mixed ? "session" : null };
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("agent", query);
        Assert.Equal(expected, result.ErrorCode);
        Assert.Equal(0, probe.IdentityCalls);
        Assert.Empty(probe.Requests);
    }

    private static Agent CreateAgent() => new Agent("agent", "Agent", "instructions", "openai/model", "test-key").UseMemory();
    private static AgentQuery Query() => new("question") { Memory = new("project", "thread") };
    private static async Task<List<AgentExecutionEvent>> Collect(IAsyncEnumerable<AgentExecutionEvent> stream)
    {
        var events = new List<AgentExecutionEvent>();
        await foreach (var item in stream) events.Add(item);
        return events;
    }
    private static IServiceCollection CreateServices(Agent agent, Probe probe, bool customExecutor = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRuniqServer(x => x.AddAgent(agent));
        services.AddRuniqMemoryInMemory();
        services.AddScoped<IMemoryIdentityResolver>(_ => probe);
        services.AddScoped<IMemoryOwnershipLookup>(_ => probe);
        services.AddScoped<IMemoryAccessPolicy>(_ => probe);
        if (customExecutor)
        {
            services.RemoveAll<IAgentExecutor>();
            services.AddScoped<IAgentExecutor>(_ => probe);
        }
        return services;
    }
    private sealed class HostIdentity : IMemoryIdentityResolver
    {
        internal MemoryIdentity? Identity;
        public ValueTask<MemoryIdentity?> ResolveAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Identity);
    }
    private sealed class Probe : IMemoryIdentityResolver, IMemoryOwnershipLookup, IMemoryAccessPolicy, IAgentExecutor
    {
        internal bool Allowed = true;
        internal string? Failure;
        internal string? CancelAt;
        internal CancellationTokenSource? Cancellation;
        internal int IdentityCalls;
        internal int Disposals;
        internal ConcurrentBag<AgentExecutionRequest> Requests = [];
        internal ConcurrentBag<AgentRunContext> Runs = [];
        public AgentExecutorKind Kind => AgentExecutorKind.Model;
        public bool SupportsMemoryFoundation { get; init; } = true;
        public ValueTask<MemoryIdentity?> ResolveAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref IdentityCalls);
            if (CancelAt == "identity") Cancellation!.Cancel();
            if (Failure == "identity") throw new InvalidOperationException("secret identity details");
            return ValueTask.FromResult<MemoryIdentity?>(Failure == "anonymous" ? null : new("caller", "tenant"));
        }
        public ValueTask<MemoryThreadOwnership?> FindAsync(string boundaryId, string threadId, CancellationToken cancellationToken)
        {
            if (CancelAt == "lookup") Cancellation!.Cancel();
            if (Failure == "lookup") throw new InvalidOperationException("secret thread details");
            return ValueTask.FromResult<MemoryThreadOwnership?>(Failure == "unknown" ? null : new(threadId, new(boundaryId, "project", "agent")));
        }
        public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken) => ValueTask.FromResult(Allowed);
        public async IAsyncEnumerable<AgentExecutionEvent> ExecuteAsync(AgentExecutionRequest request, AgentRunContext run,
            AgentToolInvoker toolInvoker, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Runs.Add(run);
            try
            {
                yield return AgentExecutionEvent.AssistantDelta("answer");
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                yield return AgentExecutionEvent.Completed();
            }
            finally { Interlocked.Increment(ref Disposals); }
        }
    }
    private sealed class AllowedClient : IChatClientResolver, IChatClient
    {
        internal int Calls;
        public IChatClient Resolve(ChatRequest request) => this;
        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(ChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new(ChatStreamingUpdateKind.ContentDelta, ContentDelta: "answer");
        }
    }

    private sealed class ForbiddenClient : IChatClientResolver, IChatClient
    {
        public IChatClient Resolve(ChatRequest request) => throw new InvalidOperationException("Provider must not resolve.");
        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Provider must not run.");
        public IAsyncEnumerable<ChatStreamingUpdate> CompleteStreamingAsync(ChatRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Provider must not run.");
    }
}
