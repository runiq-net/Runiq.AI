using Runiq.AI.Memory.Validation;

namespace Runiq.AI.Memory.Models;

/// <summary>Identifies the durable outcome of one conversation turn.</summary>
public enum MemoryTurnStatus
{
    /// <summary>The turn owns the conversation; a process interruption leaves this state unfinished.</summary>
    Running,
    /// <summary>All output and the terminal state were committed together.</summary>
    Completed,
    /// <summary>The turn failed; its transcript is diagnostic only.</summary>
    Failed,
    /// <summary>The caller cancelled or abandoned the stream; output is incomplete.</summary>
    Cancelled
}

/// <summary>Associates a logical turn with its invocation, stable history boundary, and durable outcome.</summary>
public sealed record MemoryTurn
{
    /// <summary>Creates an immutable lifecycle snapshot.</summary>
    /// <param name="turnId">The caller-retained logical request identifier within the thread.</param>
    /// <param name="runId">The invocation that reserved the turn.</param>
    /// <param name="historyVersion">The last message sequence before the user input.</param>
    /// <param name="startedAt">The original start timestamp.</param>
    /// <param name="status">The current durable state.</param>
    /// <param name="endedAt">The terminal timestamp, required exactly for terminal states.</param>
    public MemoryTurn(string turnId, string runId, long historyVersion, DateTimeOffset startedAt,
        MemoryTurnStatus status = MemoryTurnStatus.Running, DateTimeOffset? endedAt = null)
    {
        TurnId = MemoryIdentifier.Validate(turnId, nameof(turnId));
        RunId = MemoryIdentifier.Validate(runId, nameof(runId));
        ArgumentOutOfRangeException.ThrowIfNegative(historyVersion);
        if (!Enum.IsDefined(status) || (status == MemoryTurnStatus.Running) != (endedAt is null) || endedAt < startedAt)
            throw new ArgumentException("Turn status and timestamps must describe a valid lifecycle.");
        HistoryVersion = historyVersion;
        StartedAt = startedAt;
        Status = status;
        EndedAt = endedAt;
    }

    /// <summary>Gets the logical turn identity; it is not an authorization capability.</summary>
    public string TurnId { get; }
    /// <summary>Gets the invocation that owns all messages in this turn.</summary>
    public string RunId { get; }
    /// <summary>Gets the inclusive upper sequence boundary of earlier history.</summary>
    public long HistoryVersion { get; }
    /// <summary>Gets the original start time.</summary>
    public DateTimeOffset StartedAt { get; }
    /// <summary>Gets the persisted lifecycle status.</summary>
    public MemoryTurnStatus Status { get; }
    /// <summary>Gets the terminal time, or null while unfinished.</summary>
    public DateTimeOffset? EndedAt { get; }
}
