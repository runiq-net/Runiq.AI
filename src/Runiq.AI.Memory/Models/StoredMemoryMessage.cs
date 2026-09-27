namespace Runiq.AI.Memory.Models;

/// <summary>Contains a message and its store-assigned ordering metadata.</summary>
/// <param name="ThreadId">The owning conversation identifier.</param>
/// <param name="Sequence">The consecutive sequence starting at one.</param>
/// <param name="PayloadVersion">The serialization format version, separate from SQL schema version.</param>
/// <param name="Content">The immutable message data.</param>
public sealed record StoredMemoryMessage(string ThreadId, long Sequence, int PayloadVersion, MemoryMessage Content);
