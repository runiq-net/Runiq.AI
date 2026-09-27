using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Models;
using System.Text;

namespace Runiq.AI.Memory.Validation;

internal static class MessageValidation
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static void Validate(ChatMessage message)
    {
        if (!Enum.IsDefined(message.Role) || message.Content is null || message.Content.Contains('\0'))
            throw new ArgumentException("A known role and non-null content without NUL characters are required.");
        // Reject malformed UTF-16 before JSON or PostgreSQL can replace it and change logical retry identity.
        StrictUtf8.GetByteCount(message.Content);
        if (message.Role == ChatRole.Tool)
            MemoryIdentifier.Validate(message.ToolCallId!, nameof(message.ToolCallId));
        else if (message.ToolCallId is not null)
            throw new ArgumentException("Only tool results may reference a tool call.");
        if (message.ToolCalls is null) return;
        if (message.Role != ChatRole.Assistant || message.ToolCalls.Count == 0)
            throw new ArgumentException("Only assistant messages may contain a nonempty tool call list.");
        foreach (var call in message.ToolCalls)
        {
            ArgumentNullException.ThrowIfNull(call);
            MemoryIdentifier.Validate(call.Id, nameof(call.Id));
            MemoryIdentifier.Validate(call.Name, nameof(call.Name));
            if (call.ArgumentsJson is null || call.ArgumentsJson.Contains('\0'))
                throw new ArgumentException("Tool arguments must be non-null and contain no NUL characters.");
            StrictUtf8.GetByteCount(call.ArgumentsJson);
        }
    }

    internal static void ValidateAppend(IEnumerable<MemoryMessage> existing, MemoryAppendRequest request)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var calls = new HashSet<(string Run, string Call)>();
        var results = new HashSet<(string Run, string Call)>();
        foreach (var message in existing) Add(message);
        foreach (var message in request.Messages) Add(message);

        void Add(MemoryMessage item)
        {
            if (!ids.Add(item.MessageId)) throw new MemoryStoreException(MemoryStoreError.MessageConflict);
            if (item.Message.ToolCallId is { } result &&
                (!calls.Contains((item.RunId, result)) || !results.Add((item.RunId, result))))
                throw new MemoryStoreException(MemoryStoreError.ToolRelationshipConflict);
            foreach (var call in item.Message.ToolCalls ?? [])
                if (!calls.Add((item.RunId, call.Id)))
                    throw new MemoryStoreException(MemoryStoreError.ToolRelationshipConflict);
        }
    }
}
