using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Abstractions;

/// <summary>Reads authoritative ownership metadata without reading conversation content.</summary>
/// <remarks>The explicitly selected store supplies this metadata adapter. Missing metadata never authorizes creation or rebinding.</remarks>
public interface IMemoryOwnershipLookup
{
    /// <summary>Finds an existing thread within a verified tenant/application boundary.</summary>
    /// <param name="boundaryId">The verified boundary restricting the lookup.</param>
    /// <param name="threadId">The requested conversation identifier.</param>
    /// <param name="cancellationToken">Cancels metadata access.</param>
    /// <returns>Authoritative metadata, or null when unavailable or absent.</returns>
    ValueTask<MemoryThreadOwnership?> FindAsync(string boundaryId, string threadId, CancellationToken cancellationToken);
}
