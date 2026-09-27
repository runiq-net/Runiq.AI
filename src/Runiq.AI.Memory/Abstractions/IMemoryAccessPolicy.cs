using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Abstractions;

/// <summary>Supplies host-owned domain membership and explicit agent-sharing decisions.</summary>
public interface IMemoryAccessPolicy
{
    /// <summary>Checks caller membership in the requested domain resource within the verified boundary.</summary>
    /// <param name="identity">The host-verified caller and tenant/application boundary.</param>
    /// <param name="resourceId">The requested user, project, or other domain resource.</param>
    /// <param name="cancellationToken">Cancels the membership check.</param>
    /// <returns>True only when the caller may access this resource.</returns>
    ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken);

    /// <summary>Checks explicit sharing-group membership for both owning and requesting agents.</summary>
    /// <param name="identity">The host-verified caller.</param>
    /// <param name="owner">The authoritative owner scope; equals requested for a new shared thread.</param>
    /// <param name="requested">The requested agent and sharing scope.</param>
    /// <param name="cancellationToken">Cancels the sharing check.</param>
    /// <returns>True only when the host permits both agents to use the group; defaults to denial.</returns>
    ValueTask<bool> CanShareAsync(MemoryIdentity identity, MemoryAccessScope owner, MemoryAccessScope requested,
        CancellationToken cancellationToken) => ValueTask.FromResult(false);
}
