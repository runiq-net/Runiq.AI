using System.Collections.Concurrent;

namespace Runiq.AI.Agents.Runtime.Claude;

/// <summary>Rejects simultaneous resumes in this host rather than interleaving persisted CLI history.</summary>
internal sealed class ClaudeSessionGate
{
    private readonly ConcurrentDictionary<string, byte> active = new(StringComparer.Ordinal);

    internal bool TryEnter(string sessionId) => active.TryAdd(sessionId, 0);
    internal void Exit(string sessionId) => active.TryRemove(sessionId, out _);
}
