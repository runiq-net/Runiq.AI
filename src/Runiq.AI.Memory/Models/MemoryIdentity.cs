using Runiq.AI.Memory.Validation;

namespace Runiq.AI.Memory.Models;

/// <summary>Represents identity verified by the host, never a client-supplied authorization DTO.</summary>
public sealed record MemoryIdentity
{
    /// <summary>Creates a verified caller identity within an explicit tenant/application boundary.</summary>
    /// <param name="callerId">The authenticated caller or trusted background-service identity.</param>
    /// <param name="boundaryId">The verified tenant, or a host-specific single-application boundary.</param>
    /// <exception cref="ArgumentException">An identifier is invalid.</exception>
    public MemoryIdentity(string callerId, string boundaryId)
    {
        CallerId = MemoryIdentifier.Validate(callerId, nameof(callerId));
        BoundaryId = MemoryIdentifier.Validate(boundaryId, nameof(boundaryId));
    }
    /// <summary>Gets the verified caller; resource membership is authorized separately.</summary>
    public string CallerId { get; }
    /// <summary>Gets the verified tenant/application boundary.</summary>
    public string BoundaryId { get; }
}
