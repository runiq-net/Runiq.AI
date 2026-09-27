using Runiq.AI.Memory.Validation;

namespace Runiq.AI.Memory.Models;

/// <summary>Contains authoritative conversation ownership metadata, never message content.</summary>
public sealed record MemoryThreadOwnership
{
    /// <summary>Creates immutable metadata for an already bound conversation.</summary>
    /// <param name="threadId">The conversation identifier.</param>
    /// <param name="scope">The authoritative owner scope.</param>
    /// <exception cref="ArgumentException">The thread identifier is invalid.</exception>
    /// <exception cref="ArgumentNullException">The scope is null.</exception>
    public MemoryThreadOwnership(string threadId, MemoryAccessScope scope)
    {
        ThreadId = MemoryIdentifier.Validate(threadId, nameof(threadId));
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }
    /// <summary>Gets the conversation identifier, never a RunId or ProviderSessionId.</summary>
    public string ThreadId { get; }
    /// <summary>Gets the bound owner scope, which must not be reassigned by a later request.</summary>
    public MemoryAccessScope Scope { get; }
}
