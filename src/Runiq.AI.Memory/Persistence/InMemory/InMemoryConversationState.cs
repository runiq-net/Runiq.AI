using System.Collections.Concurrent;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Persistence.InMemory;

// The singleton contains data only; scoped host policies are never captured here.
internal sealed class InMemoryConversationState
{
    internal ConcurrentDictionary<(string Boundary, string Thread), Entry> Conversations { get; } = new();

    internal sealed class Entry(MemoryConversation conversation)
    {
        internal object Gate { get; } = new();
        internal MemoryConversation Conversation { get; set; } = conversation;
        internal List<StoredMemoryMessage> Messages { get; } = [];
        internal Dictionary<string, (string Payload, MemoryAppendResult Result)> Receipts { get; } = new(StringComparer.Ordinal);
    }
}
