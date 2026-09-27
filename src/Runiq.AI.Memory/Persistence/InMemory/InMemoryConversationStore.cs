using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Serialization;
using Runiq.AI.Memory.Services;
using Runiq.AI.Memory.Validation;

namespace Runiq.AI.Memory.Persistence.InMemory;

internal sealed class InMemoryConversationStore(InMemoryConversationState state, IMemoryAccessPolicy policy)
    : IMemoryConversationStore, IMemoryOwnershipLookup
{

    public ValueTask<MemoryThreadOwnership?> FindAsync(string boundaryId, string threadId, CancellationToken cancellationToken)
    {
        MemoryIdentifier.Validate(boundaryId, nameof(boundaryId));
        MemoryIdentifier.Validate(threadId, nameof(threadId));
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(state.Conversations.TryGetValue((boundaryId, threadId), out var entry)
            ? entry.Conversation.Ownership : null);
    }

    public async ValueTask<MemoryConversation> CreateAsync(MemoryContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await MemoryStoreValidation.DemandAsync(policy, context, context.Ownership, cancellationToken);
        var key = (context.Identity.BoundaryId, context.Ownership.ThreadId);
        if (!context.IsNewThread && !state.Conversations.ContainsKey(key))
            throw new MemoryStoreException(MemoryStoreError.AccessDenied);
        cancellationToken.ThrowIfCancellationRequested();
        var entry = state.Conversations.GetOrAdd(key,
            _ => new(new(context.Ownership, DateTimeOffset.UtcNow, 0)));
        lock (entry.Gate)
        {
            if (entry.Conversation.Ownership != context.Ownership)
                throw new MemoryStoreException(MemoryStoreError.OwnershipConflict);
            return entry.Conversation;
        }
    }

    public async ValueTask<MemoryConversation> ReadAsync(MemoryContext context, CancellationToken cancellationToken = default)
    {
        var entry = await GetAuthorizedAsync(context, cancellationToken);
        lock (entry.Gate) return entry.Conversation;
    }

    public async ValueTask<IReadOnlyList<MemoryConversation>> ListAsync(MemoryContext context, string? afterThreadId = null,
        int limit = 100, CancellationToken cancellationToken = default)
    {
        MemoryStoreValidation.Page(limit, afterThreadId: afterThreadId);
        await GetAuthorizedAsync(context, cancellationToken);
        var result = new List<MemoryConversation>();
        foreach (var entry in state.Conversations.Values
            .Where(e => MemoryStoreValidation.Matches(context, e.Conversation.Ownership) &&
                (afterThreadId is null || string.CompareOrdinal(e.Conversation.Ownership.ThreadId, afterThreadId) > 0))
            .OrderBy(e => e.Conversation.Ownership.ThreadId, StringComparer.Ordinal))
        {
            // Host sharing membership can differ per owning agent; filter denied candidates before taking the page.
            if (!await MemoryAuthorizationService.CanAccessOwnershipAsync(policy, context.Identity, context.AccessScope,
                entry.Conversation.Ownership, cancellationToken)) continue;
            lock (entry.Gate) result.Add(entry.Conversation);
            if (result.Count == limit) break;
        }
        return result.AsReadOnly();
    }

    public async ValueTask<IReadOnlyList<StoredMemoryMessage>> ReadMessagesAsync(MemoryContext context, long afterSequence = 0,
        int limit = 100, CancellationToken cancellationToken = default)
    {
        MemoryStoreValidation.Page(limit, afterSequence);
        var entry = await GetAuthorizedAsync(context, cancellationToken);
        lock (entry.Gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Array.AsReadOnly(entry.Messages.Where(m => m.Sequence > afterSequence).Take(limit).ToArray());
        }
    }

    public async ValueTask<MemoryAppendResult> AppendAsync(MemoryContext context, MemoryAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entry = await GetAuthorizedAsync(context, cancellationToken);
        var payload = MemoryMessageSerializer.RequestPayload(request);
        lock (entry.Gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Ownership cannot be rebound. All mutation and duplicate/version decisions share this gate.
            if (entry.Receipts.TryGetValue(request.IdempotencyKey, out var receipt))
                return receipt.Payload == payload ? receipt.Result : throw new MemoryStoreException(MemoryStoreError.IdempotencyConflict);
            if (entry.Conversation.Version != request.ExpectedVersion)
                throw new MemoryStoreException(MemoryStoreError.VersionConflict);
            MessageValidation.ValidateAppend(entry.Messages.Select(m => m.Content), request);
            var result = new MemoryAppendResult(checked(request.ExpectedVersion + 1), checked(request.ExpectedVersion + request.Messages.Count));
            var batch = request.Messages.Select((m, i) => new StoredMemoryMessage(context.Ownership.ThreadId,
                result.FirstSequence + i, MemoryMessageSerializer.CurrentVersion, m)).ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            entry.Messages.AddRange(batch);
            entry.Receipts.Add(request.IdempotencyKey, (payload, result));
            entry.Conversation = entry.Conversation with { Version = result.Version };
            return result;
        }
    }

    private async ValueTask<InMemoryConversationState.Entry> GetAuthorizedAsync(MemoryContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        state.Conversations.TryGetValue((context.Identity.BoundaryId, context.Ownership.ThreadId), out var entry);
        await MemoryStoreValidation.DemandAsync(policy, context, entry?.Conversation.Ownership, cancellationToken);
        return entry!;
    }
}
