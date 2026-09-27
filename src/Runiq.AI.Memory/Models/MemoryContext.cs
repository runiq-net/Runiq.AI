using Runiq.AI.Memory.Configuration;

namespace Runiq.AI.Memory.Models;

/// <summary>Contains the immutable result of a successful foundation authorization decision.</summary>
/// <remarks>This is not a storage capability. Stores recheck authoritative ownership and host policy on each operation.</remarks>
public sealed class MemoryContext
{
    internal MemoryContext(MemoryIdentity identity, MemoryThreadOwnership ownership, MemoryAccessScope accessScope,
        MemoryScope scope, bool isNewThread)
    {
        Identity = identity;
        Ownership = ownership;
        AccessScope = accessScope;
        Scope = scope;
        IsNewThread = isNewThread;
    }
    /// <summary>Gets the verified caller for this invocation.</summary>
    public MemoryIdentity Identity { get; }
    /// <summary>Gets existing authoritative ownership or the proposed binding for a new thread.</summary>
    public MemoryThreadOwnership Ownership { get; }
    /// <summary>Gets the requesting agent's authorized tenant/resource/sharing scope.</summary>
    public MemoryAccessScope AccessScope { get; }
    /// <summary>Gets the intended retrieval boundary; no history is loaded by this foundation.</summary>
    public MemoryScope Scope { get; }
    /// <summary>Gets whether the selected store must atomically create this proposed thread binding.</summary>
    public bool IsNewThread { get; }
}
