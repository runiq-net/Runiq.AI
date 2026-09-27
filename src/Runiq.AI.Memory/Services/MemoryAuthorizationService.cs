using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Services;

/// <summary>Enforces tenant, resource, authoritative thread ownership, and explicit agent sharing.</summary>
public sealed class MemoryAuthorizationService
{
    private readonly IMemoryOwnershipLookup ownershipLookup;
    private readonly IMemoryAccessPolicy accessPolicy;

    /// <summary>Creates the neutral authorization service from host-supplied metadata and membership adapters.</summary>
    /// <param name="ownershipLookup">Reads authoritative metadata only, never message content.</param>
    /// <param name="accessPolicy">Checks host-owned resource and sharing membership.</param>
    /// <exception cref="ArgumentNullException">A required adapter is null.</exception>
    public MemoryAuthorizationService(IMemoryOwnershipLookup ownershipLookup, IMemoryAccessPolicy accessPolicy)
    {
        this.ownershipLookup = ownershipLookup ?? throw new ArgumentNullException(nameof(ownershipLookup));
        this.accessPolicy = accessPolicy ?? throw new ArgumentNullException(nameof(accessPolicy));
    }

    /// <summary>Authorizes existing ownership or proposes a new binding without reading or storing messages.</summary>
    /// <param name="identity">The host-verified caller, never rehydrated client authorization data.</param>
    /// <param name="reference">The untrusted requested resource and optional existing thread.</param>
    /// <param name="agentId">The configured executing agent.</param>
    /// <param name="options">Immutable Memory scope settings.</param>
    /// <param name="cancellationToken">Cancels metadata and host policy calls.</param>
    /// <returns>An immutable authorized context, or null for every form of access denial.</returns>
    /// <exception cref="ArgumentException">The settings or agent identifier are invalid.</exception>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public async ValueTask<MemoryContext?> AuthorizeAsync(MemoryIdentity identity, MemoryReference reference,
        string agentId, MemoryOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var requested = new MemoryAccessScope(identity.BoundaryId, reference.ResourceId, agentId, options.SharingGroup);
        var resourceAllowed = await accessPolicy.CanAccessResourceAsync(identity, reference.ResourceId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!resourceAllowed) return null;

        MemoryThreadOwnership ownership;
        if (reference.ThreadId is null)
        {
            // A generated proposal is not persistence. #200 must atomically create and bind it.
            ownership = new MemoryThreadOwnership(Guid.NewGuid().ToString("N"), requested);
        }
        else
        {
            var existing = await ownershipLookup.FindAsync(identity.BoundaryId, reference.ThreadId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (existing is null || existing.ThreadId != reference.ThreadId ||
                existing.Scope.BoundaryId != requested.BoundaryId || existing.Scope.ResourceId != requested.ResourceId)
                return null;
            ownership = existing;
        }

        var owner = ownership.Scope;
        if (owner.SharingGroup != requested.SharingGroup) return null;
        if (requested.SharingGroup is null)
        {
            if (owner.AgentId != requested.AgentId) return null;
        }
        else
        {
            var sharingAllowed = await accessPolicy.CanShareAsync(identity, owner, requested, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!sharingAllowed) return null;
        }
        return new MemoryContext(identity, ownership, requested, options.Scope, reference.ThreadId is null);
    }
}
