using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Configuration;

namespace Runiq.AI.Memory.Services;

/// <summary>Projects already-authorized, replay-eligible history into complete, bounded message groups.</summary>
/// <remarks>Considers newest groups first, skips oversized or malformed groups intact, and returns original chronological order.
/// This pure projection performs no store operations and does not establish replay eligibility.</remarks>
public static class MemoryHistorySelector
{
    /// <summary>Selects original messages using caller-supplied costs from the overall prompt accounting policy.</summary>
    /// <param name="history">Chronological messages already filtered by completed turns and the reservation boundary.</param>
    /// <param name="options">Identity-free limits applied together with the allocation.</param>
    /// <param name="allocatedTokens">Nonnegative token capacity remaining after higher-priority prompt content.</param>
    /// <param name="messageCosts">One nonnegative cost per message, including tool payloads and message framing.</param>
    /// <returns>Original messages in chronological order, without truncation or partial tool interactions.</returns>
    /// <exception cref="ArgumentNullException">A required input is null.</exception>
    /// <exception cref="ArgumentException">The costs do not match the history or contain negative values.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The allocation is negative.</exception>
    public static IReadOnlyList<ChatMessage> Select(IReadOnlyList<ChatMessage> history,
        MemoryHistoryOptions options, int allocatedTokens, IReadOnlyList<int> messageCosts)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(messageCosts);
        if (allocatedTokens < 0) throw new ArgumentOutOfRangeException(nameof(allocatedTokens));
        if (history.Count != messageCosts.Count || messageCosts.Any(cost => cost < 0))
            throw new ArgumentException("One nonnegative cost is required per history message.", nameof(messageCosts));

        var groups = new List<(int Start, int Count, long Cost)>();
        for (var index = 0; index < history.Count; index++)
        {
            var message = history[index];
            if (message.Role == ChatRole.Tool) continue; // Orphan results are never model context.
            var start = index;
            var valid = true;
            if (message.ToolCalls is { Count: > 0 } calls)
            {
                var pending = calls.Select(call => call.Id).ToHashSet(StringComparer.Ordinal);
                valid = message.Role == ChatRole.Assistant && pending.Count == calls.Count;
                while (index + 1 < history.Count && history[index + 1].Role == ChatRole.Tool)
                {
                    index++;
                    valid &= history[index].ToolCallId is { } id && pending.Remove(id);
                }
                valid &= pending.Count == 0;
            }
            if (!valid) continue;
            long cost = 0;
            for (var item = start; item <= index; item++) cost += messageCosts[item];
            groups.Add((start, index - start + 1, cost));
        }

        var tokens = Math.Min(allocatedTokens, options.MaximumTokens ?? int.MaxValue);
        var messages = options.MaximumMessages ?? int.MaxValue;
        if (tokens == 0 || messages == 0) return [];
        var retained = new List<(int Start, int Count)>();
        for (var index = groups.Count - 1; index >= 0; index--)
        {
            var group = groups[index];
            if (group.Count > messages || group.Cost > tokens) continue;
            retained.Add((group.Start, group.Count));
            messages -= group.Count;
            tokens -= (int)group.Cost;
        }
        retained.Reverse();
        return retained.SelectMany(group => Enumerable.Range(group.Start, group.Count).Select(index => history[index])).ToArray();
    }
}
