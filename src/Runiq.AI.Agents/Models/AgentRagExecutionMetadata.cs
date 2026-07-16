using Runiq.AI.Agents.Configuration;

namespace Runiq.AI.Agents;

/// <summary>
/// Describes the framework-observed RAG policy outcome for an agent execution.
/// </summary>
public sealed class AgentRagExecutionMetadata
{
    internal AgentRagExecutionMetadata(
        RagExecutionMode mode,
        bool hasAcceptedContext,
        RagNoContextBehavior? appliedNoContextBehavior,
        RagNoContextReason? noContextReason,
        bool modelInvocationSkipped,
        bool isAnswerGrounded)
    {
        Mode = mode;
        HasAcceptedContext = hasAcceptedContext;
        AppliedNoContextBehavior = appliedNoContextBehavior;
        NoContextReason = noContextReason;
        ModelInvocationSkipped = modelInvocationSkipped;
        IsAnswerGrounded = isAnswerGrounded;
    }

    /// <summary>
    /// Gets the RAG execution mode applied by the framework.
    /// </summary>
    public RagExecutionMode Mode { get; }

    /// <summary>
    /// Gets a value indicating whether retrieval produced context accepted by the runtime policy.
    /// </summary>
    public bool HasAcceptedContext { get; }

    /// <summary>
    /// Gets the no-context behavior applied by the framework, or null when accepted context was available.
    /// </summary>
    public RagNoContextBehavior? AppliedNoContextBehavior { get; }

    /// <summary>
    /// Gets the verifiable no-context reason, or null when context was accepted or retrieval failed.
    /// </summary>
    public RagNoContextReason? NoContextReason { get; }

    /// <summary>
    /// Gets a value indicating whether the framework skipped model/provider invocation.
    /// </summary>
    public bool ModelInvocationSkipped { get; }

    /// <summary>
    /// Gets a value indicating whether the framework constrained the final model response to accepted context.
    /// This reports the applied policy and does not independently verify model semantics.
    /// </summary>
    public bool IsAnswerGrounded { get; }
}
