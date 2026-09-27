namespace Runiq.AI.Memory.Models;

/// <summary>Describes the original committed sequence range; exact retries return this same receipt.</summary>
/// <param name="FirstSequence">The first sequence assigned to the batch; Version + 1 for a terminal-only empty batch.</param>
/// <param name="Version">The final sequence and conversation version after this batch.</param>
public sealed record MemoryAppendResult(long FirstSequence, long Version);
