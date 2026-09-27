using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Abstractions;

/// <summary>Persists conversations with authoritative scope checks and atomic, ordered appends.</summary>
/// <remarks>Contexts are revalidated per operation. Denied or absent content raises AccessDenied.
/// Cancellation before commit leaves no writes; an uncertain commit must be retried with the original request.
/// Providers never silently fall back to another store. Turn state is committed with messages and receipts.</remarks>
public interface IMemoryConversationStore
{
    /// <summary>Atomically creates the proposed ownership at version zero, or returns the identical existing binding.</summary>
    /// <param name="context">The verified new-thread proposal or authorized existing context.</param>
    /// <param name="cancellationToken">Cancels before commit.</param>
    /// <returns>The persisted conversation; incompatible rebinding raises OwnershipConflict.</returns>
    ValueTask<MemoryConversation> CreateAsync(MemoryContext context, CancellationToken cancellationToken = default);

    /// <summary>Reads the authorized context's conversation after checking its current ownership.</summary>
    /// <param name="context">The host-authorized context, revalidated against storage.</param>
    /// <param name="cancellationToken">Cancels the read and policy checks.</param>
    /// <returns>The immutable conversation snapshot.</returns>
    ValueTask<MemoryConversation> ReadAsync(MemoryContext context, CancellationToken cancellationToken = default);

    /// <summary>Lists visible conversations in ascending ordinal ThreadId order using an exclusive cursor.</summary>
    /// <param name="context">Thread scope returns only its thread; resource scope permits matching resource/agent/sharing.</param>
    /// <param name="afterThreadId">The last returned identifier, or null for the first page.</param>
    /// <param name="limit">The page size from one through 1000. An empty page ends traversal.</param>
    /// <param name="cancellationToken">Cancels enumeration and policy checks.</param>
    /// <returns>Isolated snapshots. Concurrent creates before the cursor appear only in a new traversal.</returns>
    ValueTask<IReadOnlyList<MemoryConversation>> ListAsync(MemoryContext context, string? afterThreadId = null,
        int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>Reads messages ordered by sequence, strictly after the supplied cursor.</summary>
    /// <param name="context">The authorized conversation context.</param>
    /// <param name="afterSequence">The exclusive sequence cursor, initially zero.</param>
    /// <param name="limit">The page size from one through 1000. An empty page ends traversal.</param>
    /// <param name="cancellationToken">Cancels reads and policy checks.</param>
    /// <returns>Immutable message snapshots; later appends may appear on subsequent pages.</returns>
    ValueTask<IReadOnlyList<StoredMemoryMessage>> ReadMessagesAsync(MemoryContext context, long afterSequence = 0,
        int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>Reads authorized lifecycle snapshots. Unfinished and unsuccessful turns must not be replayed as completed output.</summary>
    /// <param name="context">The context revalidated against authoritative ownership.</param>
    /// <param name="cancellationToken">Cancels the read and access checks.</param>
    /// <returns>Immutable turn snapshots; legacy messages have no associated turn.</returns>
    ValueTask<IReadOnlyList<MemoryTurn>> ReadTurnsAsync(MemoryContext context, CancellationToken cancellationToken = default);

    /// <summary>Commits all messages and the retry receipt together after authoritative authorization.</summary>
    /// <param name="context">The authorized conversation context.</param>
    /// <param name="request">The stable key, required expected version, and ordered payload.</param>
    /// <param name="cancellationToken">Cancels policy calls and storage work before commit.</param>
    /// <returns>The original receipt on exact retry, checked before version conflicts. New requests compete on version.</returns>
    /// <exception cref="MemoryStoreException">Access, key/payload, version, identity, or tool relationships conflict.</exception>
    ValueTask<MemoryAppendResult> AppendAsync(MemoryContext context, MemoryAppendRequest request,
        CancellationToken cancellationToken = default);
}
