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
    // A captured but authoritatively denied reservation must not invent a turn or attempt an unauthorized terminal write.
    public async Task ConversationService_InitialAppendRejectionNeedsNoFinalizationWrite()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var service = scope.ServiceProvider.GetRequiredService<MemoryConversationService>();
        var context = Proposal();
        await service.CreateAsync(context);
        MemoryTurnSession? captured = null;
        await Error(MemoryStoreError.AccessDenied, () => service.BeginAsync(context, "turn", "run", "question", DateTimeOffset.UtcNow,
            session => { captured = session; Policy.Allow = false; }, CancellationToken.None).AsTask());
        Assert.NotNull(captured);
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await captured.FinishAsync(MemoryTurnStatus.Failed, cleanup.Token);
        Policy.Allow = true;
        Assert.Empty(await store.ReadTurnsAsync(context));
        Assert.Empty(await store.ReadMessagesAsync(context));
        Assert.Equal(0, (await store.ReadAsync(context)).Version);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Both providers reconcile the original startup request after commit cancellation or two lost acknowledgements, then release the reservation.
    public async Task ConversationService_InitialAppendRecoveryRetainsSession(bool cancelAfterCommit)
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var uncertain = new UncertainStore(store);
        using var caller = new CancellationTokenSource();
        var tokens = new List<CancellationToken>();
        uncertain.AfterAppend = (_, token) =>
        {
            tokens.Add(token);
            if (cancelAfterCommit && tokens.Count == 1)
            {
                caller.Cancel();
                throw new OperationCanceledException(caller.Token);
            }
            if (!cancelAfterCommit && tokens.Count <= 2) throw new MemoryStoreException(MemoryStoreError.StorageFailure);
            return ValueTask.CompletedTask;
        };
        var service = new MemoryConversationService(uncertain);
        var context = Proposal();
        await service.CreateAsync(context);
        MemoryTurnSession? captured = null;
        var beginning = service.BeginAsync(context, "turn", "run", "question", DateTimeOffset.UtcNow,
            session => { Assert.Empty(uncertain.Attempts); captured = session; }, caller.Token).AsTask();
        if (cancelAfterCommit)
            Assert.Equal(caller.Token, (await Assert.ThrowsAnyAsync<OperationCanceledException>(() => beginning)).CancellationToken);
        else
            Assert.Equal(MemoryStoreError.StorageFailure, (await Assert.ThrowsAsync<MemoryStoreException>(() => beginning)).Error);
        Assert.NotNull(captured);
        Assert.Equal(MemoryTurnStatus.Running, Assert.Single(await store.ReadTurnsAsync(context)).Status);
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var outcome = cancelAfterCommit ? MemoryTurnStatus.Cancelled : MemoryTurnStatus.Failed;
        await captured.FinishAsync(outcome, cleanup.Token);
        Assert.Equal(cancelAfterCommit ? 3 : 4, uncertain.Attempts.Count);
        var original = uncertain.Attempts[0];
        Assert.All(uncertain.Attempts.Take(uncertain.Attempts.Count - 1), request => Assert.Same(original, request));
        Assert.Equal(cleanup.Token, tokens[^2]);
        Assert.Equal(cleanup.Token, tokens[^1]);
        Assert.NotEqual(caller.Token, tokens[^2]);
        Assert.Equal(outcome, Assert.Single(await store.ReadTurnsAsync(context)).Status);
        Assert.Equal(original.Messages[0], Assert.Single(await store.ReadMessagesAsync(context)).Content);
        Assert.Equal(1, (await store.ReadAsync(context)).Version);
        var next = await service.BeginAsync(context, "next-turn", "next-run", "next input", DateTimeOffset.UtcNow);
        await next.FinishAsync(MemoryTurnStatus.Completed);
        Assert.Equal(2, (await store.ReadMessagesAsync(context)).Count);
        Assert.Equal(2, (await store.ReadAsync(context)).Version);
        Assert.DoesNotContain(await store.ReadTurnsAsync(context), t => t.Status == MemoryTurnStatus.Running);
    }

    [Fact]
    // An uncertain commit retries the very same immutable append rather than regenerating its key, time, or payload.
    public async Task ConversationService_RetriesUncertainCommitWithoutDuplicateMessages()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var uncertain = new UncertainStore(store);
        var service = new MemoryConversationService(uncertain);
        var context = Proposal();
        await service.CreateAsync(context);
        var session = await service.BeginAsync(context, "turn", "run", "question", DateTimeOffset.UtcNow);
        session.AppendDelta("answer");
        await session.FinishAsync(MemoryTurnStatus.Completed);
        Assert.Equal(4, uncertain.Attempts.Count);
        Assert.Same(uncertain.Attempts[0], uncertain.Attempts[1]);
        Assert.Same(uncertain.Attempts[2], uncertain.Attempts[3]);
        Assert.Equal(2, (await store.ReadMessagesAsync(context)).Count);
        Assert.Equal(MemoryTurnStatus.Completed, Assert.Single(await store.ReadTurnsAsync(context)).Status);
        var original = uncertain.Attempts[0];
        await Error(MemoryStoreError.IdempotencyConflict, () => store.AppendAsync(context,
            new(original.IdempotencyKey, original.ExpectedVersion, [Message("changed")], original.Turn)).AsTask());
    }

    private sealed class UncertainStore(IMemoryConversationStore inner) : IMemoryConversationStore
    {
        internal List<MemoryAppendRequest> Attempts = [];
        internal Func<MemoryAppendRequest, CancellationToken, ValueTask>? AfterAppend;
        private readonly HashSet<string> committed = [];
        public ValueTask<MemoryConversation> CreateAsync(MemoryContext c, CancellationToken t = default) => inner.CreateAsync(c, t);
        public ValueTask<MemoryConversation> ReadAsync(MemoryContext c, CancellationToken t = default) => inner.ReadAsync(c, t);
        public ValueTask<IReadOnlyList<MemoryConversation>> ListAsync(MemoryContext c, string? afterThreadId = null, int limit = 100, CancellationToken cancellationToken = default) => inner.ListAsync(c, afterThreadId, limit, cancellationToken);
        public ValueTask<IReadOnlyList<StoredMemoryMessage>> ReadMessagesAsync(MemoryContext c, long afterSequence = 0, int limit = 100, CancellationToken cancellationToken = default) => inner.ReadMessagesAsync(c, afterSequence, limit, cancellationToken);
        public ValueTask<IReadOnlyList<MemoryTurn>> ReadTurnsAsync(MemoryContext c, CancellationToken t = default) => inner.ReadTurnsAsync(c, t);
        public async ValueTask<MemoryAppendResult> AppendAsync(MemoryContext c, MemoryAppendRequest r, CancellationToken t = default)
        {
            Attempts.Add(r);
            var result = await inner.AppendAsync(c, r, t);
            if (AfterAppend is not null) await AfterAppend(r, t);
            else if (committed.Add(r.IdempotencyKey)) throw new MemoryStoreException(MemoryStoreError.StorageFailure);
            return result;
        }
    }

    [Fact]
    // Neutral orchestration pages completed history, excludes partial turns, and never re-adds current input.
    public async Task ConversationService_LoadsOnlyCompletedHistoryAcrossPages()
    {
        using var scope = Host.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryConversationService>();
        var context = Proposal();
        await service.CreateAsync(context);
        for (var i = 0; i < 51; i++)
        {
            var session = await service.BeginAsync(context, $"turn-{i}", $"run-{i}", $"user-{i}", DateTimeOffset.UtcNow);
            session.AppendDelta($"answer-{i}");
            await session.FinishAsync(MemoryTurnStatus.Completed);
        }
        var failed = await service.BeginAsync(context, "failed", "failed", "failed input", DateTimeOffset.UtcNow);
        failed.AppendDelta("incomplete");
        await failed.FinishAsync(MemoryTurnStatus.Failed);
        var current = await service.BeginAsync(context, "current", "current", "current", DateTimeOffset.UtcNow);
        var history = await current.LoadHistoryAsync();
        Assert.Equal(102, history.Count);
        Assert.Equal("user-0", history[0].Content);
        Assert.Equal("answer-50", history[^1].Content);
        Assert.DoesNotContain(history, m => m.Content is "incomplete" or "current" or "failed input");
    }

    [Theory]
    [InlineData(MemoryTurnStatus.Completed)]
    [InlineData(MemoryTurnStatus.Failed)]
    [InlineData(MemoryTurnStatus.Cancelled)]
    // Lifecycle writes are atomic, exact retries retain receipts, and terminal states remain distinct.
    public async Task TurnLifecycle_PreservesAtomicWritesAndRetries(MemoryTurnStatus status)
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        await store.CreateAsync(context);
        var turn = new MemoryTurn("turn", "run", 0, Message("u").Timestamp);
        var start = new MemoryAppendRequest("start", 0, [Message("u")], turn);
        var first = await store.AppendAsync(context, start);
        Assert.Equal(turn, Assert.Single(await store.ReadTurnsAsync(context)));
        var end = new MemoryTurn(turn.TurnId, turn.RunId, 0, turn.StartedAt, status, turn.StartedAt.AddSeconds(1));
        var terminal = new MemoryAppendRequest("end", 1, [Message("a", chat: new(ChatRole.Assistant, "answer"))], end);
        var receipt = await store.AppendAsync(context, terminal);
        Assert.Equal(receipt, await store.AppendAsync(context, terminal));
        Assert.Equal(first, await store.AppendAsync(context, start));
        Assert.Equal(end, Assert.Single(await store.ReadTurnsAsync(context)));
        Assert.Equal(2, (await store.ReadMessagesAsync(context)).Count);
        await Error(MemoryStoreError.TurnConflict, () => store.AppendAsync(context,
            new("late", 2, [Message("late", chat: new(ChatRole.Assistant, "late"))], turn)).AsTask());
    }

    [Fact]
    // Durable reservations reject overlaps and untracked writes; cancellation before commit leaves the turn running.
    public async Task RunningTurn_RejectsOverlapAndSupportsEmptyFinalization()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        await store.CreateAsync(context);
        var turn = new MemoryTurn("turn", "run", 0, Message("u").Timestamp);
        await store.AppendAsync(context, new("start", 0, [Message("u")], turn));
        using var other = Host.CreateScope();
        var competing = other.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        await Error(MemoryStoreError.TurnConflict, () => competing.AppendAsync(context,
            new("overlap", 1, [Message("other", "other")], new("other", "other", 1, turn.StartedAt))).AsTask());
        await Error(MemoryStoreError.TurnConflict, () => competing.AppendAsync(context, new("legacy", 1, [Message("legacy", "legacy")])).AsTask());
        var finish = new MemoryAppendRequest("cancel", 1, [], new("turn", "run", 0, turn.StartedAt, MemoryTurnStatus.Cancelled, turn.StartedAt.AddSeconds(1)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.AppendAsync(context, finish, cancellation.Token).AsTask());
        Assert.Equal(MemoryTurnStatus.Running, Assert.Single(await store.ReadTurnsAsync(context)).Status);
        var receipt = await store.AppendAsync(context, finish);
        Assert.Equal(new MemoryAppendResult(2, 1), receipt);
        Assert.Equal(receipt, await store.AppendAsync(context, finish));
        await Error(MemoryStoreError.AccessDenied, () => store.ReadTurnsAsync(Proposal(resource: "denied")).AsTask());
    }

    [Fact]
    // Pending tools cannot be marked complete but remain available for diagnosing failed runs.
    public async Task TurnCompletion_RejectsIncompleteToolInteractions()
    {
        using var scope = Host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        var context = Proposal();
        await store.CreateAsync(context);
        var turn = new MemoryTurn("turn", "run", 0, Message("u").Timestamp);
        await store.AppendAsync(context, new("start", 0, [Message("u")], turn));
        await store.AppendAsync(context, new("tool", 1, [Message("pending", chat: new(ChatRole.Assistant, "working", ToolCalls: [new("call", "tool", "{}")]))], turn));
        await Error(MemoryStoreError.ToolRelationshipConflict, () => store.AppendAsync(context,
            new("done", 2, [], new("turn", "run", 0, turn.StartedAt, MemoryTurnStatus.Completed, turn.StartedAt.AddSeconds(1)))).AsTask());
        Assert.Equal(MemoryTurnStatus.Running, Assert.Single(await store.ReadTurnsAsync(context)).Status);
        await store.AppendAsync(context, new("failed", 2, [], new("turn", "run", 0, turn.StartedAt, MemoryTurnStatus.Failed, turn.StartedAt.AddSeconds(1))));
    }

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
