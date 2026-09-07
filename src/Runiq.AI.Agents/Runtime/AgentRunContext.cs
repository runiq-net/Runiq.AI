namespace Runiq.AI.Agents.Runtime;

/// <summary>Describes the lifecycle state of one runtime invocation.</summary>
public enum AgentRunStatus
{
    /// <summary>The invocation has started and has not reached a terminal state.</summary>
    Running = 0,
    /// <summary>The invocation completed successfully.</summary>
    Completed = 1,
    /// <summary>The invocation ended with an execution failure.</summary>
    Failed = 2,
    /// <summary>The caller cancelled or disposed the unfinished invocation.</summary>
    Cancelled = 3
}

/// <summary>Holds runtime-owned identity and lifecycle state for a single invocation.</summary>
public sealed class AgentRunContext
{
    private readonly object lifecycleLock = new();
    private AgentRunStatus status;
    private DateTimeOffset? endedAt;
    private long eventSequence;

    internal long NextEventSequence() => Interlocked.Increment(ref eventSequence);

    internal AgentRunContext(string agentId)
    {
        AgentId = agentId;
        RunId = Guid.NewGuid().ToString("N");
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Gets the opaque run identifier, which is never a session-resumption key.</summary>
    public string RunId { get; }

    /// <summary>Gets the reusable agent definition identifier.</summary>
    public string AgentId { get; }

    /// <summary>Gets the UTC time when runtime created this invocation's context, on first enumeration for streams.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Gets the UTC time of the first terminal transition, or null while the run is running.</summary>
    /// <remarks>The timestamp is assigned with the terminal state and is never changed by later completion attempts.</remarks>
    public DateTimeOffset? EndedAt
    {
        get { lock (lifecycleLock) return endedAt; }
    }

    /// <summary>Gets the reserved provider session identifier; always null in this version.</summary>
    public string? ProviderSessionId => null;

    /// <summary>Gets the current state; only the runtime can make a terminal transition.</summary>
    public AgentRunStatus Status
    {
        get { lock (lifecycleLock) return status; }
    }

    internal void Finish(AgentRunStatus terminalStatus)
    {
        if (terminalStatus is not (AgentRunStatus.Completed or AgentRunStatus.Failed or AgentRunStatus.Cancelled))
            throw new ArgumentException("A terminal state is required.", nameof(terminalStatus));
        // Publish terminal state and its timestamp together so a terminal reader cannot observe a missing end time.
        lock (lifecycleLock)
        {
            if (status != AgentRunStatus.Running) return;
            endedAt = DateTimeOffset.UtcNow;
            status = terminalStatus;
        }
    }
}

/// <summary>Reports caller cancellation with the identity of the cancelled runtime invocation.</summary>
public sealed class AgentRunCanceledException : OperationCanceledException
{
    internal AgentRunCanceledException(AgentRunContext run, CancellationToken cancellationToken,
        OperationCanceledException? innerException = null)
        : base("Agent execution was cancelled.", innerException, cancellationToken)
    {
        Run = run;
    }

    /// <summary>Gets the runtime context in its terminal Cancelled state.</summary>
    public AgentRunContext Run { get; }
}
