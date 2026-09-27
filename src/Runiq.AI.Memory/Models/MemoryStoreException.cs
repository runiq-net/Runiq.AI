namespace Runiq.AI.Memory.Models;

/// <summary>Identifies provider-neutral persistence failures.</summary>
public enum MemoryStoreError
{
    /// <summary>The context cannot access the authoritative owner, or the conversation is absent.</summary>
    AccessDenied,
    /// <summary>The proposed binding differs from an existing immutable owner.</summary>
    OwnershipConflict,
    /// <summary>The request key was committed with a different logical payload.</summary>
    IdempotencyConflict,
    /// <summary>The expected version is stale.</summary>
    VersionConflict,
    /// <summary>A message identifier already exists in this conversation.</summary>
    MessageConflict,
    /// <summary>A tool relationship is unresolved, duplicated, or belongs to another run.</summary>
    ToolRelationshipConflict,
    /// <summary>A stored payload is malformed or uses an unsupported format.</summary>
    InvalidPayload,
    /// <summary>The database schema is incompatible with this provider.</summary>
    IncompatibleSchema,
    /// <summary>Storage failed; retry the original request after resolving the underlying failure.</summary>
    StorageFailure
}

/// <summary>Reports a neutral storage outcome without requiring a database client dependency.</summary>
public sealed class MemoryStoreException : Exception
{
    /// <summary>Creates a safe diagnostic for a persistence failure.</summary>
    /// <param name="error">The stable failure category.</param>
    /// <param name="innerException">The underlying diagnostic, if any; do not expose it to clients.</param>
    public MemoryStoreException(MemoryStoreError error, Exception? innerException = null)
        : base($"Memory storage operation failed: {error}.", innerException) => Error = error;

    /// <summary>Gets the stable provider-independent failure category.</summary>
    public MemoryStoreError Error { get; }
}
