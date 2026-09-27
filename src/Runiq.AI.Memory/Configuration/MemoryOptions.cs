using Runiq.AI.Memory.Validation;

namespace Runiq.AI.Memory.Configuration;

/// <summary>Selects the content boundary for future Memory retrieval.</summary>
public enum MemoryScope
{
    /// <summary>Limits content to the authorized conversation.</summary>
    Thread,
    /// <summary>Allows future recall across authorized threads in the same resource and agent/sharing scope.</summary>
    Resource
}

/// <summary>Contains immutable, identity-free settings safe to share across agent invocations.</summary>
public sealed record MemoryOptions
{
    /// <summary>Creates explicit Memory opt-in settings without selecting a persistence provider.</summary>
    /// <param name="scope">The intended retrieval boundary.</param>
    /// <param name="sharingGroup">An explicit sharing group requiring a host policy, or null for isolation.</param>
    /// <exception cref="ArgumentException">The scope or sharing identifier is invalid.</exception>
    public MemoryOptions(MemoryScope scope = MemoryScope.Thread, string? sharingGroup = null)
    {
        if (!Enum.IsDefined(scope)) throw new ArgumentException("Unsupported Memory scope.", nameof(scope));
        Scope = scope;
        SharingGroup = sharingGroup is null ? null : MemoryIdentifier.Validate(sharingGroup, nameof(sharingGroup));
    }
    /// <summary>Gets the intended thread or resource retrieval boundary.</summary>
    public MemoryScope Scope { get; }
    /// <summary>Gets the explicit host sharing group, without implying caller or agent membership.</summary>
    public string? SharingGroup { get; }

    /// <summary>Validates a snapshot at configuration and execution boundaries.</summary>
    /// <exception cref="ArgumentException">The settings are invalid.</exception>
    public void Validate()
    {
        if (!Enum.IsDefined(Scope)) throw new ArgumentException("Unsupported Memory scope.");
        if (SharingGroup is not null) MemoryIdentifier.Validate(SharingGroup, nameof(SharingGroup));
    }
}
