using Runiq.AI.Memory.Validation;

namespace Runiq.AI.Memory.Models;

/// <summary>Identifies requested Memory content without granting access to it.</summary>
/// <remarks>Identifiers are ordinal, case-sensitive, and never trimmed. Null ThreadId requests a new conversation.</remarks>
public sealed record MemoryReference
{
    /// <summary>Creates an untrusted conversation reference for later host authorization.</summary>
    /// <param name="resourceId">The user, project, or domain resource owning the conversation.</param>
    /// <param name="threadId">An existing conversation identifier, or null to request a new thread.</param>
    /// <exception cref="ArgumentException">An identifier is empty, too long, or contains forbidden whitespace.</exception>
    public MemoryReference(string resourceId, string? threadId = null)
    {
        ResourceId = MemoryIdentifier.Validate(resourceId, nameof(resourceId));
        ThreadId = threadId is null ? null : MemoryIdentifier.Validate(threadId, nameof(threadId));
    }

    /// <summary>Gets the requested owner resource; this is not necessarily the authenticated caller.</summary>
    public string ResourceId { get; }

    /// <summary>Gets the conversation identifier, independent of invocation and provider session identifiers.</summary>
    public string? ThreadId { get; }
}
