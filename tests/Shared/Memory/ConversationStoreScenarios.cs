using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Services;

namespace Runiq.AI.Memory.Tests.Persistence;

// Linked as test source by each provider; no production test-support package or database dependency is required.
public abstract class ConversationStoreScenarios : IAsyncLifetime
{
    protected ServiceProvider Host = null!;
    protected readonly TestPolicy Policy = new();
    protected abstract ServiceProvider CreateHost();
    public virtual Task InitializeAsync() { Host = CreateHost(); return Task.CompletedTask; }
    public virtual async Task DisposeAsync() => await Host.DisposeAsync();

    protected static MemoryMessage Message(string id, string run = "run", ChatMessage? chat = null) =>
        new(id, run, new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.FromHours(3)).AddTicks(1234567), chat ?? new(ChatRole.User, id));

    protected static MemoryContext Proposal(string thread = "thread", string tenant = "tenant", string resource = "resource",
        string agent = "agent", string? group = null, MemoryScope scope = MemoryScope.Thread) =>
        new(new("caller", tenant), new(thread, new(tenant, resource, agent, group)), new(tenant, resource, agent, group), scope, true);

    [Fact]
    // The same contract creates, binds, reads, and pages ordered messages without losing timestamps or tool links.
    public async Task CreateAppendRead_PreservesMetadataAndPagination()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        var created = await store.CreateAsync(context);
        Assert.Equal(0, created.Version);
        Assert.Equal(created, await store.CreateAsync(context));
        var messages = new[] { Message("u"), Message("a", chat: new(ChatRole.Assistant, "", ToolCalls: [new("call", "tool", "{}")])),
            Message("t", chat: new(ChatRole.Tool, "result", "call")) };
        Assert.Equal(new(1, 3), await store.AppendAsync(context, new("key", 0, messages)));
        var page = await store.ReadMessagesAsync(context, limit: 2);
        Assert.Equal(new long[] { 1, 2 }, page.Select(m => m.Sequence));
        var last = Assert.Single(await store.ReadMessagesAsync(context, page[^1].Sequence, 2));
        Assert.Equal("thread", last.ThreadId);
        Assert.Equal("run", last.Content.RunId);
        Assert.Equal(messages[2].Timestamp.Ticks, last.Content.Timestamp.Ticks);
        Assert.Equal(messages[2].Timestamp.Offset, last.Content.Timestamp.Offset);
        Assert.Equal("call", last.Content.Message.ToolCallId);
        Assert.Equal(1, last.PayloadVersion);
        Assert.Empty(await store.ReadMessagesAsync(context, 3));
        Assert.Equal(3, (await store.ReadAsync(context)).Version);
    }

    [Fact]
    // Exact retries precede stale-version checks and retain their original result after subsequent appends.
    public async Task RetriesAndConflicts_AreAtomicAndDistinct()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        await store.CreateAsync(context);
        var request = new MemoryAppendRequest("key", 0, [Message("one")]);
        var result = await store.AppendAsync(context, request);
        await store.AppendAsync(context, new("next", 1, [Message("two")]));
        Assert.Equal(result, await store.AppendAsync(context, request));
        await Error(MemoryStoreError.IdempotencyConflict, () => store.AppendAsync(context, new("key", 0, [Message("changed")])).AsTask());
        await Error(MemoryStoreError.VersionConflict, () => store.AppendAsync(context, new("stale", 0, [Message("three")])).AsTask());
        await Error(MemoryStoreError.MessageConflict, () => store.AppendAsync(context, new("duplicate", 2, [Message("three"), Message("one")])).AsTask());
        Assert.Equal(2, (await store.ReadAsync(context)).Version);
        Assert.Equal(2, (await store.ReadMessagesAsync(context)).Count);
    }

    [Fact]
    // Dedicated workers cross a common barrier before competing for one version or the same retry receipt.
    public async Task ConcurrentWrites_RespectExpectedVersionAndRetryIdentity()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        await store.CreateAsync(context);
        var identical = new MemoryAppendRequest("same", 0, [Message("one")]);
        var receipts = await RunConcurrentlyAsync(8, async _ =>
        {
            using var other = Host.CreateScope();
            return await other.ServiceProvider.GetRequiredService<IMemoryConversationStore>().AppendAsync(context, identical);
        });
        Assert.All(receipts, r => Assert.Equal(new(1, 1), r));
        var outcomes = await RunConcurrentlyAsync(8, async i =>
        {
            using var other = Host.CreateScope();
            try
            {
                await other.ServiceProvider.GetRequiredService<IMemoryConversationStore>().AppendAsync(context, new("k" + i, 1, [Message("m" + i)]));
                return true;
            }
            catch (MemoryStoreException e) when (e.Error == MemoryStoreError.VersionConflict) { return false; }
        });
        Assert.Single(outcomes, x => x);
        Assert.Equal(2, (await store.ReadAsync(context)).Version);
    }

    [Fact]
    // Tenant, resource, agent, and thread boundaries are enforced even for manufactured or previously issued contexts.
    public async Task IsolationAndRevocation_ProtectEveryContentOperation()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        await store.CreateAsync(context);
        await store.AppendAsync(context, new("key", 0, [Message("one")]));
        await store.CreateAsync(Proposal(tenant: "other"));
        Assert.Empty(await store.ReadMessagesAsync(Proposal(tenant: "other")));
        foreach (var denied in new[] { Proposal(resource: "other"), Proposal(agent: "other"), Proposal(tenant: "missing") })
        {
            await Error(MemoryStoreError.AccessDenied, () => store.ReadAsync(denied).AsTask());
            await Error(MemoryStoreError.AccessDenied, () => store.ReadMessagesAsync(denied).AsTask());
            await Error(MemoryStoreError.AccessDenied, () => store.ListAsync(denied).AsTask());
            await Error(MemoryStoreError.AccessDenied, () => store.AppendAsync(denied, new("key", 0, [Message("one")])).AsTask());
        }
        Policy.Allow = false;
        await Error(MemoryStoreError.AccessDenied, () => store.ReadAsync(context).AsTask());
        await Error(MemoryStoreError.AccessDenied, () => store.CreateAsync(context).AsTask());
        await Error(MemoryStoreError.AccessDenied, () => store.AppendAsync(context, new("key", 0, [Message("one")])).AsTask());
    }

    [Fact]
    // Dedicated workers start incompatible bindings together; exactly one immutable resource owner may win.
    public async Task CompetingBindings_HaveOneOwner()
    {
        var outcomes = await RunConcurrentlyAsync(2, async index =>
        {
            var resource = index == 0 ? "first" : "second";
            using var scope = Host.CreateScope();
            try { await scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>().CreateAsync(Proposal(resource: resource)); return resource; }
            catch (MemoryStoreException e) when (e.Error == MemoryStoreError.OwnershipConflict) { return null; }
        });
        var winner = Assert.Single(outcomes, x => x is not null);
        using var readScope = Host.CreateScope();
        var lookup = readScope.ServiceProvider.GetRequiredService<IMemoryOwnershipLookup>();
        Assert.Equal(winner, (await lookup.FindAsync("tenant", "thread", default))!.Scope.ResourceId);
    }

    [Fact]
    // Resource pagination uses exclusive ordinal keys and checks explicit sharing membership per owning agent.
    public async Task ListingAndSharing_KeepDeterministicScopedPages()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        foreach (var id in new[] { "z", "A", "a" }) await store.CreateAsync(Proposal(id, scope: MemoryScope.Resource));
        await store.CreateAsync(Proposal("hidden", resource: "other"));
        var context = Proposal("A", scope: MemoryScope.Resource);
        Assert.Equal(new[] { "A", "a" }, (await store.ListAsync(context, limit: 2)).Select(c => c.Ownership.ThreadId));
        Assert.Equal("z", Assert.Single(await store.ListAsync(context, "a", 2)).Ownership.ThreadId);
        Assert.Empty(await store.ListAsync(context, "z"));
        Assert.Single(await store.ListAsync(Proposal("A")));
        var shared = Proposal("shared", group: "team");
        await store.CreateAsync(shared);
        var requesting = new MemoryContext(shared.Identity, shared.Ownership, new("tenant", "resource", "other", "team"), MemoryScope.Thread, false);
        Assert.Equal("shared", (await store.ReadAsync(requesting)).Ownership.ThreadId);
        Policy.Share = false;
        await Error(MemoryStoreError.AccessDenied, () => store.ReadAsync(requesting).AsTask());
    }

    [Fact]
    // Tool results require a unique earlier call in the same run; failed batches leave pending calls untouched.
    public async Task ToolRelationships_AreValidatedAcrossBatches()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        await store.CreateAsync(context);
        await store.AppendAsync(context, new("call", 0, [Message("a", chat: new(ChatRole.Assistant, "", ToolCalls: [new("c", "tool", "{}")] ))]));
        await Error(MemoryStoreError.ToolRelationshipConflict, () => store.AppendAsync(context,
            new("wrong", 1, [Message("t", "other-run", new(ChatRole.Tool, "result", "c"))])).AsTask());
        await Error(MemoryStoreError.ToolRelationshipConflict, () => store.AppendAsync(context,
            new("missing", 1, [Message("t", chat: new(ChatRole.Tool, "result", "missing"))])).AsTask());
        await store.AppendAsync(context, new("result", 1, [Message("t", chat: new(ChatRole.Tool, "result", "c"))]));
        await Error(MemoryStoreError.ToolRelationshipConflict, () => store.AppendAsync(context,
            new("twice", 2, [Message("t2", chat: new(ChatRole.Tool, "result", "c"))])).AsTask());
        Assert.Equal(2, (await store.ReadAsync(context)).Version);
    }

    [Fact]
    // Cancelled writes do not create conversations, consume retry keys, or change the visible version.
    public async Task CancellationAndSnapshots_DoNotMutateStorage()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        var cancelled = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CreateAsync(context, cancelled).AsTask());
        await store.CreateAsync(context);
        var calls = new List<ChatToolCall> { new("c", "tool", "{}") };
        var message = Message("a", chat: new(ChatRole.Assistant, "", ToolCalls: calls));
        var batch = new List<MemoryMessage> { message };
        var request = new MemoryAppendRequest("key", 0, batch);
        calls.Clear(); batch.Clear();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.AppendAsync(context, request, cancelled).AsTask());
        Assert.Equal(0, (await store.ReadAsync(context)).Version);
        await store.AppendAsync(context, request);
        Assert.Single(Assert.Single(await store.ReadMessagesAsync(context)).Content.Message.ToolCalls!);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadMessagesAsync(context, cancellationToken: cancelled).AsTask());
    }

    protected static async Task Error(MemoryStoreError expected, Func<Task> action) =>
        Assert.Equal(expected, (await Assert.ThrowsAsync<MemoryStoreException>(action)).Error);

    [Fact]
    // Unicode cursor order must match .NET ordinal comparison rather than provider-specific text collation.
    public async Task UnicodeIdentifiers_HaveIdenticalOrdinalPagination()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var ids = new[] { "a", "A", "é", "é", "\U00010000", "\uE000", "thread" };
        foreach (var id in ids) await store.CreateAsync(Proposal(id, scope: MemoryScope.Resource));
        var context = Proposal(scope: MemoryScope.Resource);
        var actual = new List<string>();
        string? cursor = null;
        while (true)
        {
            var page = await store.ListAsync(context, cursor, 2);
            if (page.Count == 0) break;
            actual.AddRange(page.Select(x => x.Ownership.ThreadId));
            cursor = page[^1].Ownership.ThreadId;
        }
        Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal), actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Full-length, diverse three-byte Unicode identifiers must support persistence and scoped ordinal pagination in every provider.
    public async Task MaximumUnicodeIdentifiers_PreserveScopedPersistence(bool shared)
    {
        // Distinct characters avoid compressible repeated strings masking an oversized database index entry.
        static string Identifier(int start) => new(Enumerable.Range(start, 256).Select(code => (char)code).ToArray());
        var tenant = Identifier(0x4E00);
        var resource = Identifier(0x5000);
        var agent = Identifier(0x5200);
        var group = shared ? Identifier(0x5400) : null;
        var first = Proposal(Identifier(0x5600), tenant, resource, agent, group, MemoryScope.Resource);
        var second = Proposal(Identifier(0x5800), tenant, resource, agent, group, MemoryScope.Resource);
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var created = await store.CreateAsync(first);
        await store.CreateAsync(second);
        await store.CreateAsync(Proposal(Identifier(0x5A00), tenant, Identifier(0x5C00), agent, group));
        await store.CreateAsync(Proposal(Identifier(0x5E00), tenant, resource,
            shared ? agent : Identifier(0x6000), shared ? Identifier(0x6200) : null));
        Assert.Equal(first.Ownership, created.Ownership);
        Assert.Equal(created, await store.ReadAsync(first));
        Assert.Equal(first.Ownership, Assert.Single(await store.ListAsync(first, limit: 1)).Ownership);
        Assert.Equal(second.Ownership,
            Assert.Single(await store.ListAsync(first, first.Ownership.ThreadId, 1)).Ownership);
        Assert.Empty(await store.ListAsync(first, second.Ownership.ThreadId));
        var message = Message(Identifier(0x6400), Identifier(0x6600));
        var request = new MemoryAppendRequest(Identifier(0x6800), 0, [message]);
        var receipt = await store.AppendAsync(first, request);
        Assert.Equal(new(1, 1), receipt);
        Assert.Equal(receipt, await store.AppendAsync(first, request));
        Assert.Equal(message, Assert.Single(await store.ReadMessagesAsync(first)).Content);
        Assert.Equal(1, (await store.ReadAsync(first)).Version);
    }

    [Fact]
    // Retry equality covers the required version, message identity/run/time/role/content, tool arguments, and batch order.
    public async Task ChangedLogicalPayload_AlwaysConflictsWithoutMutation()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        await store.CreateAsync(context);
        var first = Message("a", chat: new(ChatRole.Assistant, "text", ToolCalls: [new("call", "tool", "{}")]));
        var second = Message("b");
        await store.AppendAsync(context, new("key", 0, [first, second]));
        var variants = new[] {
            new MemoryAppendRequest("key", 1, [first, second]),
            new MemoryAppendRequest("key", 0, [second, first]),
            new MemoryAppendRequest("key", 0, [new("changed", first.RunId, first.Timestamp, first.Message), second]),
            new MemoryAppendRequest("key", 0, [new(first.MessageId, "other-run", first.Timestamp, first.Message), second]),
            new MemoryAppendRequest("key", 0, [new(first.MessageId, first.RunId, first.Timestamp.AddTicks(1), first.Message), second]),
            new MemoryAppendRequest("key", 0, [new(first.MessageId, first.RunId, first.Timestamp.ToUniversalTime(), first.Message), second]),
            new MemoryAppendRequest("key", 0, [new(first.MessageId, first.RunId, first.Timestamp, first.Message with { Content = "changed" }), second]),
            new MemoryAppendRequest("key", 0, [new(first.MessageId, first.RunId, first.Timestamp, new(ChatRole.User, "text")), second]),
            new MemoryAppendRequest("key", 0, [new(first.MessageId, first.RunId, first.Timestamp, first.Message with { ToolCalls = [new("call", "tool", "{ }")] }), second]) };
        foreach (var request in variants)
            await Error(MemoryStoreError.IdempotencyConflict, () => store.AppendAsync(context, request).AsTask());
        Assert.Equal(2, (await store.ReadAsync(context)).Version);
    }

    [Fact]
    // Sharing is checked for each stored owner before page limits, so denied owners neither leak nor hide later allowed items.
    public async Task SharingPolicy_FiltersEachCandidateBeforePagination()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        await store.CreateAsync(Proposal("anchor", group: "team", scope: MemoryScope.Resource));
        await store.CreateAsync(Proposal("a-denied", agent: "blocked", group: "team"));
        await store.CreateAsync(Proposal("z-visible", agent: "allowed", group: "team"));
        Policy.DeniedOwner = "BLOCKED";
        var context = Proposal("anchor", group: "team", scope: MemoryScope.Resource);
        Assert.Equal("anchor", Assert.Single(await store.ListAsync(context, limit: 1)).Ownership.ThreadId);
        Assert.Equal("z-visible", Assert.Single(await store.ListAsync(context, "anchor", 1)).Ownership.ThreadId);
        await Error(MemoryStoreError.AccessDenied, () => store.ReadAsync(Proposal("a-denied", agent: "blocked", group: "team")).AsTask());
    }

    [Fact]
    // An invalid relationship late in the batch and competing payloads under one key cannot produce partial messages.
    public async Task InvalidBatchAndConflictingRetryRaces_AreAtomic()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        await store.CreateAsync(context);
        await Error(MemoryStoreError.ToolRelationshipConflict, () => store.AppendAsync(context, new("invalid", 0,
            [Message("first"), Message("bad", chat: new(ChatRole.Tool, "result", "missing"))])).AsTask());
        Assert.Empty(await store.ReadMessagesAsync(context));
        var outcomes = await Task.WhenAll(new[] { "one", "two" }.Select(async id =>
        {
            using var other = Host.CreateScope();
            try
            {
                await other.ServiceProvider.GetRequiredService<IMemoryConversationStore>().AppendAsync(context, new("race", 0, [Message(id)]));
                return true;
            }
            catch (MemoryStoreException e) when (e.Error == MemoryStoreError.IdempotencyConflict) { return false; }
        }));
        Assert.Single(outcomes, x => x);
        Assert.Single(await store.ReadMessagesAsync(context));
    }

    // LongRunning supplies dedicated threads: synchronously completed ValueTasks cannot serialize worker creation,
    // and waiting at the barrier cannot starve the thread pool. The timeout only bounds a broken rendezvous.
    private static async Task<T[]> RunConcurrentlyAsync<T>(int workerCount, Func<int, Task<T>> operation)
    {
        using var barrier = new Barrier(workerCount);
        var workers = Enumerable.Range(0, workerCount).Select(index => Task.Factory.StartNew(async () =>
        {
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(30)), "Concurrent workers did not reach the start barrier.");
            return await operation(index);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap()).ToArray();
        return await Task.WhenAll(workers);
    }

    protected sealed class TestPolicy : IMemoryAccessPolicy
    {
        public bool Allow = true;
        public bool Share = true;
        public string? DeniedOwner;
        public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken) => ValueTask.FromResult(Allow);
        public ValueTask<bool> CanShareAsync(MemoryIdentity identity, MemoryAccessScope owner, MemoryAccessScope requested, CancellationToken cancellationToken) => ValueTask.FromResult(Share && owner.AgentId != DeniedOwner);
    }
}
