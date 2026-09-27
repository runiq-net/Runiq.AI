using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.Services;

namespace Runiq.AI.Memory.Validation;

internal static class MemoryStoreValidation
{
    internal static void Page(int limit, long afterSequence = 0, string? afterThreadId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        if (afterThreadId is not null) MemoryIdentifier.Validate(afterThreadId, nameof(afterThreadId));
    }

    internal static async ValueTask DemandAsync(IMemoryAccessPolicy policy, MemoryContext context,
        MemoryThreadOwnership? owner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (owner is null || owner.ThreadId != context.Ownership.ThreadId ||
            !await MemoryAuthorizationService.CanAccessOwnershipAsync(policy, context.Identity, context.AccessScope, owner, cancellationToken))
            throw new MemoryStoreException(MemoryStoreError.AccessDenied);
    }

    internal static bool Matches(MemoryContext context, MemoryThreadOwnership owner) =>
        context.Identity.BoundaryId == owner.Scope.BoundaryId &&
        context.AccessScope.BoundaryId == owner.Scope.BoundaryId &&
        context.AccessScope.ResourceId == owner.Scope.ResourceId &&
        context.AccessScope.SharingGroup == owner.Scope.SharingGroup &&
        (owner.Scope.SharingGroup is not null || context.AccessScope.AgentId == owner.Scope.AgentId) &&
        (context.Scope == MemoryScope.Resource || context.Ownership.ThreadId == owner.ThreadId);
}
