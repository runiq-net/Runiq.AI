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
    private int status;
    private long eventSequence;

    internal long NextEventSequence() => Interlocked.Increment(ref eventSequence);

    internal AgentRunContext(string agentId)
    {
        AgentId = agentId;
        RunId = Guid.NewGuid().ToString("N");
    }

    /// <summary>Gets the opaque run identifier, which is never a session-resumption key.</summary>
    public string RunId { get; }

    /// <summary>Gets the reusable agent definition identifier.</summary>
    public string AgentId { get; }

    /// <summary>Gets the reserved provider session identifier; always null in this version.</summary>
    public string? ProviderSessionId => null;

    /// <summary>Gets the current state; only the runtime can make a terminal transition.</summary>
    public AgentRunStatus Status => (AgentRunStatus)Volatile.Read(ref status);

    internal void Finish(AgentRunStatus terminalStatus)
    {
        if (terminalStatus == AgentRunStatus.Running)
            throw new ArgumentException("A terminal state is required.", nameof(terminalStatus));
        Interlocked.CompareExchange(ref status, (int)terminalStatus, (int)AgentRunStatus.Running);
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
