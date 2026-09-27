namespace Runiq.AI.Memory.Models;

/// <summary>Describes an immutable conversation snapshot.</summary>
/// <param name="Ownership">The atomically bound, immutable owner.</param>
/// <param name="CreatedAt">The store-assigned UTC creation time.</param>
/// <param name="Version">The last assigned message sequence, initially zero.</param>
public sealed record MemoryConversation(MemoryThreadOwnership Ownership, DateTimeOffset CreatedAt, long Version);
