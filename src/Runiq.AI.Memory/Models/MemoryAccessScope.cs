using Runiq.AI.Memory.Validation;

namespace Runiq.AI.Memory.Models;

/// <summary>Defines a structural ownership boundary without concatenating identifiers.</summary>
public sealed record MemoryAccessScope
{
    /// <summary>Creates a tenant/resource/agent ownership scope.</summary>
    /// <param name="boundaryId">The verified tenant or explicit single-application boundary.</param>
    /// <param name="resourceId">The resource owning the content.</param>
    /// <param name="agentId">The owning agent; normalized to uppercase to match agent registration semantics.</param>
    /// <param name="sharingGroup">An explicit host sharing group, or null for agent isolation.</param>
    /// <exception cref="ArgumentException">An identifier is invalid.</exception>
    public MemoryAccessScope(string boundaryId, string resourceId, string agentId, string? sharingGroup = null)
    {
        BoundaryId = MemoryIdentifier.Validate(boundaryId, nameof(boundaryId));
        ResourceId = MemoryIdentifier.Validate(resourceId, nameof(resourceId));
        AgentId = MemoryIdentifier.Validate(agentId, nameof(agentId)).ToUpperInvariant();
        SharingGroup = sharingGroup is null ? null : MemoryIdentifier.Validate(sharingGroup, nameof(sharingGroup));
    }

    /// <summary>Gets the tenant or application boundary, compared ordinally.</summary>
    public string BoundaryId { get; }
    /// <summary>Gets the owner resource, compared ordinally.</summary>
    public string ResourceId { get; }
    /// <summary>Gets the normalized owning agent identifier.</summary>
    public string AgentId { get; }
    /// <summary>Gets the explicit sharing group; membership still requires host authorization.</summary>
    public string? SharingGroup { get; }
}
