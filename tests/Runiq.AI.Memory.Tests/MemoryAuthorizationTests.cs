using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.DependencyInjection;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Services;

namespace Runiq.AI.Memory.Tests;

public sealed class MemoryAuthorizationTests
{
    [Theory]
    [InlineData("tenant", "project", "agent", null, true, false, true)]
    [InlineData("other-tenant", "project", "agent", null, true, false, false)]
    [InlineData("tenant", "other-project", "agent", null, true, false, false)]
    [InlineData("tenant", "project", "other-agent", null, true, false, false)]
    [InlineData("tenant", "project", "agent", null, false, false, false)]
    [InlineData("tenant", "project", "other-agent", "support", true, true, true)]
    [InlineData("tenant", "project", "other-agent", "support", true, false, false)]
    [InlineData("other-tenant", "project", "other-agent", "support", true, true, false)]
    // Host membership never overrides tenant, resource, or default agent isolation checks.
    public async Task ExistingThread_EnforcesOwnershipMatrix(string boundary, string resource, string ownerAgent,
        string? group, bool resourceAllowed, bool sharingAllowed, bool expected)
    {
        var lookup = new Lookup(new MemoryThreadOwnership("thread", new MemoryAccessScope(boundary, resource, ownerAgent, group)));
        var policy = new Policy(resourceAllowed, sharingAllowed);
        var service = new MemoryAuthorizationService(lookup, policy);
        var result = await service.AuthorizeAsync(new MemoryIdentity("caller", "tenant"), new MemoryReference("project", "thread"),
            "agent", new MemoryOptions(sharingGroup: group));
        Assert.Equal(expected, result is not null);
        if (!resourceAllowed) Assert.Equal(0, lookup.Calls);
        if (result is not null)
        {
            Assert.False(result.IsNewThread);
            Assert.Same(lookup.Value, result.Ownership);
            Assert.Equal("AGENT", result.AccessScope.AgentId);
        }
    }

    [Fact]
    // Unknown or inconsistent metadata cannot be used to create or rebind an existing thread.
    public async Task MissingOrInconsistentMetadata_FailsClosed()
    {
        foreach (var ownership in new MemoryThreadOwnership?[] { null,
                     new("wrong-thread", new("tenant", "project", "agent")),
                     new("thread", new("tenant", "project", "agent", "other-group")) })
        {
            var service = new MemoryAuthorizationService(new Lookup(ownership), new Policy(true, true));
            Assert.Null(await service.AuthorizeAsync(new("caller", "tenant"), new("project", "thread"), "agent", new()));
        }
    }

    [Fact]
    // New-thread approval generates a distinct proposal without pretending metadata has been persisted.
    public async Task NewThreads_AreExplicitUnpersistedProposals()
    {
        var lookup = new Lookup(null);
        var service = new MemoryAuthorizationService(lookup, new Policy(true, false));
        var first = await service.AuthorizeAsync(new("caller", "tenant"), new("project"), "agent", new(MemoryScope.Resource));
        var second = await service.AuthorizeAsync(new("caller", "tenant"), new("project"), "agent", new());
        Assert.True(first!.IsNewThread);
        Assert.Equal(MemoryScope.Resource, first.Scope);
        Assert.NotEqual(first.Ownership.ThreadId, second!.Ownership.ThreadId);
        Assert.Equal(0, lookup.Calls);
        Assert.Null(await service.AuthorizeAsync(new("caller", "tenant"), new("project"), "agent", new(sharingGroup: "support")));
    }

    [Fact]
    // Cancellation is observed even when a host adapter completes without throwing on a cancelled token.
    public async Task Cancellation_StopsAuthorization()
    {
        using var cancellation = new CancellationTokenSource();
        var lookup = new Lookup(null) { OnCall = cancellation.Cancel };
        var service = new MemoryAuthorizationService(lookup, new Policy(true, false));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.AuthorizeAsync(new("caller", "tenant"), new("project", "thread"), "agent", new(), cancellation.Token));
        Assert.Equal(cancellation.Token, lookup.Token);
    }

    [Fact]
    // Neutral registration is idempotent, scoped, and does not supply an implicit identity or store.
    public void Registration_RequiresExplicitHostAdapters()
    {
        var services = new ServiceCollection().AddRuniqMemory().AddRuniqMemory();
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, d => d.ServiceType == typeof(MemoryAuthorizationService)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, d => d.ServiceType == typeof(MemoryConversationService)).Lifetime);
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IMemoryIdentityResolver>());
        Assert.Null(provider.GetService<IMemoryConversationStore>());
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<MemoryConversationService>());
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<MemoryAuthorizationService>());
    }

    private sealed class Lookup(MemoryThreadOwnership? value) : IMemoryOwnershipLookup
    {
        internal MemoryThreadOwnership? Value = value;
        internal int Calls;
        internal Action? OnCall;
        internal CancellationToken Token;
        public ValueTask<MemoryThreadOwnership?> FindAsync(string boundaryId, string threadId, CancellationToken cancellationToken)
        {
            Calls++;
            Token = cancellationToken;
            OnCall?.Invoke();
            return ValueTask.FromResult(Value);
        }
    }

    [Fact]
    // A resource-only policy cannot accidentally opt into sharing through the interface default method.
    public async Task Sharing_RequiresExplicitHostImplementation()
    {
        var service = new MemoryAuthorizationService(new Lookup(null), new ResourceOnlyPolicy());
        Assert.Null(await service.AuthorizeAsync(new("caller", "tenant"), new("project"), "agent", new(sharingGroup: "support")));
    }

    private sealed class ResourceOnlyPolicy : IMemoryAccessPolicy
    {
        public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);
    }
    private sealed class Policy(bool resourceAllowed, bool sharingAllowed) : IMemoryAccessPolicy
    {
        public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(resourceAllowed);
        public ValueTask<bool> CanShareAsync(MemoryIdentity identity, MemoryAccessScope owner, MemoryAccessScope requested,
            CancellationToken cancellationToken) => ValueTask.FromResult(sharingAllowed);
    }
}
