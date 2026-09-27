using Runiq.AI.Memory.Validation;

namespace Runiq.AI.Memory.Models;

/// <summary>Describes an all-or-nothing append with durable retry identity.</summary>
public sealed class MemoryAppendRequest
{
    /// <summary>Snapshots a nonempty message batch using the original append contract.</summary>
    /// <param name="idempotencyKey">The stable tenant/thread-scoped request key.</param>
    /// <param name="expectedVersion">The original required conversation version.</param>
    /// <param name="messages">The nonempty ordered message batch.</param>
    public MemoryAppendRequest(string idempotencyKey, long expectedVersion, IReadOnlyList<MemoryMessage> messages)
        : this(idempotencyKey, expectedVersion, messages, null) { }

    /// <summary>Snapshots a message batch and optional lifecycle mutation before storage work.</summary>
    /// <param name="idempotencyKey">The stable tenant/thread-scoped request key.</param>
    /// <param name="expectedVersion">The version observed before this request; required even for retries.</param>
    /// <param name="messages">The ordered batch; terminal-only transitions may be empty. Identifiers must be unique within the batch.</param>
    /// <param name="turn">Optional lifecycle mutation committed atomically with messages and the receipt.</param>
    /// <exception cref="ArgumentException">The key, version, or batch is invalid.</exception>
    public MemoryAppendRequest(string idempotencyKey, long expectedVersion, IReadOnlyList<MemoryMessage> messages, MemoryTurn? turn)
    {
        IdempotencyKey = MemoryIdentifier.Validate(idempotencyKey, nameof(idempotencyKey));
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        ArgumentNullException.ThrowIfNull(messages);
        var snapshot = messages.ToArray();
        if ((snapshot.Length == 0 && (turn is null || turn.Status == MemoryTurnStatus.Running)) || snapshot.Any(m => m is null))
            throw new ArgumentException("An append requires messages or a terminal transition, without null messages.", nameof(messages));
        if (snapshot.Select(m => m.MessageId).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new ArgumentException("Message identifiers must be unique within a batch.", nameof(messages));
        Turn = turn;
        ExpectedVersion = expectedVersion;
        Messages = Array.AsReadOnly(snapshot);
    }

    /// <summary>Gets the lifecycle mutation; a terminal-only append may have no messages.</summary>
    public MemoryTurn? Turn { get; }

    /// <summary>Gets the stable request key scoped to tenant and conversation.</summary>
    public string IdempotencyKey { get; }
    /// <summary>Gets the required original version, included in logical request equality.</summary>
    public long ExpectedVersion { get; }
    /// <summary>Gets the immutable ordered batch.</summary>
    public IReadOnlyList<MemoryMessage> Messages { get; }
}
