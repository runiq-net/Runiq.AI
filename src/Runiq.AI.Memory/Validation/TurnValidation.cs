using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Validation;

internal static class TurnValidation
{
    internal static void ValidateAppend(IEnumerable<MemoryTurn> turns, IEnumerable<MemoryMessage> messages, MemoryAppendRequest request)
    {
        var snapshots = turns.ToArray();
        var running = snapshots.SingleOrDefault(t => t.Status == MemoryTurnStatus.Running);
        if (request.Turn is not { } turn)
        {
            if (running is not null || request.Messages.Any(m => snapshots.Any(t => t.RunId == m.RunId)))
                throw new MemoryStoreException(MemoryStoreError.TurnConflict);
            return;
        }

        var previous = snapshots.SingleOrDefault(t => t.TurnId == turn.TurnId);
        if (request.Messages.Any(m => m.RunId != turn.RunId) || (running is not null && running.TurnId != turn.TurnId))
            throw new MemoryStoreException(MemoryStoreError.TurnConflict);
        if (previous is null)
        {
            if (turn.Status != MemoryTurnStatus.Running || turn.HistoryVersion != request.ExpectedVersion ||
                request.Messages.Count != 1 || request.Messages[0].Message.Role != ChatRole.User ||
                snapshots.Any(t => t.RunId == turn.RunId) || messages.Any(m => m.RunId == turn.RunId))
                throw new MemoryStoreException(MemoryStoreError.TurnConflict);
        }
        else if (previous.Status != MemoryTurnStatus.Running || previous.RunId != turn.RunId ||
            previous.HistoryVersion != turn.HistoryVersion || !previous.StartedAt.EqualsExact(turn.StartedAt) ||
            request.Messages.Any(m => m.Message.Role is ChatRole.User or ChatRole.System))
            throw new MemoryStoreException(MemoryStoreError.TurnConflict);

        if (turn.Status == MemoryTurnStatus.Completed)
        {
            var transcript = messages.Concat(request.Messages).Where(m => m.RunId == turn.RunId).ToArray();
            var calls = transcript.SelectMany(m => m.Message.ToolCalls ?? []).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
            var results = transcript.Where(m => m.Message.Role == ChatRole.Tool).Select(m => m.Message.ToolCallId!).ToHashSet(StringComparer.Ordinal);
            if (!calls.SetEquals(results)) throw new MemoryStoreException(MemoryStoreError.ToolRelationshipConflict);
        }
    }
}
