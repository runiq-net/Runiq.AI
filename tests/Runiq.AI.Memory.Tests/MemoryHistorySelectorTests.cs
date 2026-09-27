using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.DependencyInjection;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Services;

namespace Runiq.AI.Memory.Tests;

public sealed class MemoryHistorySelectorTests
{
    [Fact]
    // Both limits and the caller allocation apply together, preferring recent messages without changing order.
    public void CombinedLimits_SelectDeterministically()
    {
        ChatMessage[] history = [new(ChatRole.User, "old"), new(ChatRole.Assistant, "middle"), new(ChatRole.User, "new")];
        var options = new MemoryHistoryOptions(2, 5);
        var selected = MemoryHistorySelector.Select(history, options, 4, [1, 2, 2]);
        Assert.Equal(history.Skip(1), selected);
        Assert.Equal(selected, MemoryHistorySelector.Select(history, options, 4, [1, 2, 2]));
        Assert.Equal([history[2]], MemoryHistorySelector.Select(history, options, 2, [1, 2, 2]));
        Assert.Equal([history[2]], MemoryHistorySelector.Select(history, new(1), 100, [1, 2, 2]));
    }

    [Fact]
    // Defaults disable only additional limits, while zero, empty, invalid and exact-fit inputs have explicit behavior.
    public void Limits_HandleBoundaries()
    {
        var options = new MemoryOptions().History;
        Assert.Null(options.MaximumMessages);
        Assert.Null(options.MaximumTokens);
        ChatMessage[] history = [new(ChatRole.User, "message")];
        Assert.Equal(history, MemoryHistorySelector.Select(history, options, 3, [3]));
        Assert.Empty(MemoryHistorySelector.Select(history, options, 2, [3]));
        Assert.Empty(MemoryHistorySelector.Select(history, options, 0, [0]));
        Assert.Empty(MemoryHistorySelector.Select(history, new(0), 3, [3]));
        Assert.Empty(MemoryHistorySelector.Select(history, new(maximumTokens: 0), 3, [3]));
        Assert.Empty(MemoryHistorySelector.Select([], options, 3, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryHistoryOptions(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryHistoryOptions(maximumTokens: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MemoryHistorySelector.Select(history, options, -1, [3]));
        Assert.Throws<ArgumentException>(() => MemoryHistorySelector.Select(history, options, 3, []));
        Assert.Throws<ArgumentException>(() => MemoryHistorySelector.Select(history, options, 3, [-1]));
    }

    [Fact]
    // Complete multi-call groups remain byte-for-byte intact and oversized groups are skipped as a unit.
    public void ToolGroups_PreservePayloadsAndSkipOversizedGroups()
    {
        ChatMessage[] history = [new(ChatRole.User, "old"),
            new(ChatRole.Assistant, "checking", ToolCalls: [new("a", "lookup", "{ \"x\": 1 }"), new("b", "other", "{}")]),
            new(ChatRole.Tool, "{ \"answer\": 42 }", "b"), new(ChatRole.Tool, "{\"ok\":true}", "a"),
            new(ChatRole.Assistant, "done")];
        var exact = MemoryHistorySelector.Select(history, new(4, 4), 4, [1, 1, 1, 1, 1]);
        Assert.Equal(history.Skip(1), exact);
        for (var index = 0; index < exact.Count; index++) Assert.Same(history[index + 1], exact[index]);
        Assert.Equal([history[0], history[4]], MemoryHistorySelector.Select(history, new(3), 100, [1, 1, 1, 1, 1]));
        Assert.Equal([history[0], history[4]], MemoryHistorySelector.Select(history, new(), 3, [1, 1, 1, 1, 1]));
    }

    [Fact]
    // Incomplete calls, duplicate results, and orphan results cannot be made replayable by selection.
    public void MalformedRelationships_AreExcluded()
    {
        ChatMessage[] history = [new(ChatRole.Tool, "orphan", "x"),
            new(ChatRole.Assistant, "incomplete", ToolCalls: [new("a", "lookup", "{}"), new("b", "lookup", "{}")]),
            new(ChatRole.Tool, "only one", "a"),
            new(ChatRole.Assistant, "duplicate", ToolCalls: [new("c", "lookup", "{}")]),
            new(ChatRole.Tool, "first", "c"), new(ChatRole.Tool, "second", "c"),
            new(ChatRole.Assistant, "unresolved", ToolCalls: [new("d", "lookup", "{}")]), new(ChatRole.User, "safe")];
        Assert.Equal([history[^1]], MemoryHistorySelector.Select(history, new(), 100, Enumerable.Repeat(1, history.Length).ToArray()));
    }

    [Fact]
    // Concurrent projections share no history, counters, or budget state.
    public async Task IndependentSelections_DoNotMixContent()
    {
        var selections = await Task.WhenAll(Enumerable.Range(1, 40).Select(index => Task.Run(() =>
            MemoryHistorySelector.Select([new(ChatRole.User, index.ToString())], new(), index, [index]))));
        Assert.Equal(Enumerable.Range(1, 40).Select(index => index.ToString()), selections.Select(items => Assert.Single(items).Content));
    }

    [Fact]
    // Projection preserves stored identities and payloads and cannot bypass completed-turn or reservation boundaries.
    public async Task Selection_LeavesAuthorizedTranscriptUnchanged()
    {
        using var host = new ServiceCollection().AddSingleton<IMemoryAccessPolicy, AllowPolicy>()
            .AddRuniqMemoryInMemory().BuildServiceProvider();
        using var scope = host.CreateScope();
        var services = scope.ServiceProvider;
        var context = (await services.GetRequiredService<MemoryAuthorizationService>().AuthorizeAsync(
            new("caller", "tenant"), new("resource"), "agent", new()))!;
        var conversations = services.GetRequiredService<MemoryConversationService>();
        await conversations.CreateAsync(context);
        var store = services.GetRequiredService<IMemoryConversationStore>();
        // Legacy content has no authoritative completed outcome.
        await store.AppendAsync(context, new("legacy", 0, [new("legacy", "unknown", DateTimeOffset.UtcNow, new(ChatRole.User, "unknown"))]));
        foreach (var status in new[] { MemoryTurnStatus.Completed, MemoryTurnStatus.Failed, MemoryTurnStatus.Cancelled })
        {
            var turn = await conversations.BeginAsync(context, status.ToString(), status.ToString(), status.ToString(), DateTimeOffset.UtcNow);
            turn.AppendDelta("answer-" + status);
            await turn.FinishAsync(status);
        }
        var active = await conversations.BeginAsync(context, "active", "active", "current", DateTimeOffset.UtcNow);
        var history = await active.LoadHistoryAsync();
        Assert.Equal(new[] { "Completed", "answer-Completed" }, history.Select(message => message.Content));
        var before = await store.ReadMessagesAsync(context);
        Assert.Equal([history[1]], MemoryHistorySelector.Select(history, new(1), 10, [1, 1]));
        Assert.Equal(before, await store.ReadMessagesAsync(context));
        Assert.Equal(8, before.Count);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active.LoadHistoryAsync(cancelled.Token).AsTask());
        await active.FinishAsync(MemoryTurnStatus.Completed);
        Assert.Equal(history, await active.LoadHistoryAsync());
    }

    private sealed class AllowPolicy : IMemoryAccessPolicy
    {
        public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }
}
