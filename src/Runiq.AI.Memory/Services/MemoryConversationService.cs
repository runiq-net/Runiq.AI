using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Services;

/// <summary>Coordinates neutral conversation history and turn writes through the selected authoritative store.</summary>
public sealed class MemoryConversationService
{
    private readonly IMemoryConversationStore store;

    /// <summary>Creates a coordinator without selecting a provider or performing I/O.</summary>
    /// <param name="store">The explicitly registered scoped conversation store.</param>
    public MemoryConversationService(IMemoryConversationStore store) => this.store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>Persists the authorized ownership proposal before exposing a reusable thread identity.</summary>
    /// <param name="context">The host-authorized proposal or existing context.</param>
    /// <param name="cancellationToken">Cancels creation before commit.</param>
    /// <returns>The persisted conversation snapshot.</returns>
    public ValueTask<MemoryConversation> CreateAsync(MemoryContext context, CancellationToken cancellationToken = default) =>
        store.CreateAsync(context, cancellationToken);

    /// <summary>Reserves a turn and appends its user input atomically before model or tool execution.</summary>
    /// <param name="context">The authorized persisted conversation.</param>
    /// <param name="turnId">The logical request identity retained by the caller.</param>
    /// <param name="runId">The fresh invocation identity.</param>
    /// <param name="input">The user input; transient instructions and evidence must not be included.</param>
    /// <param name="startedAt">The invocation start timestamp.</param>
    /// <param name="cancellationToken">Cancels reads and reservation before commit.</param>
    /// <returns>A sequential, invocation-owned turn session.</returns>
    /// <remarks>Duplicate invocations and overlaps are rejected before model execution. No invocation is automatically rerun.
    /// Use the overload with captureSession when the caller must finalize an uncertain initial append after this call throws.</remarks>
    public ValueTask<MemoryTurnSession> BeginAsync(MemoryContext context, string turnId, string runId,
        string input, DateTimeOffset startedAt, CancellationToken cancellationToken = default) =>
        BeginAsync(context, turnId, runId, input, startedAt, static _ => { }, cancellationToken);

    /// <summary>Exposes the recovery session before attempting the atomic user-input and turn reservation.</summary>
    /// <param name="context">The authorized persisted conversation.</param>
    /// <param name="turnId">The caller-retained logical turn identity.</param>
    /// <param name="runId">The fresh invocation identity.</param>
    /// <param name="input">The user input without transient instructions or evidence.</param>
    /// <param name="startedAt">The invocation's original start timestamp.</param>
    /// <param name="captureSession">Retains the session before the first write; do not operate on it until this call returns or throws.</param>
    /// <param name="cancellationToken">Cancels reads and reservation; an uncertain write remains in the captured session.</param>
    /// <returns>The same session after the reservation receipt is confirmed.</returns>
    /// <remarks>When this call fails, the captured session can reconcile its original pending request and finalize
    /// with a bounded independent token. Preflight failures do not capture a session or reserve a turn.</remarks>
    public async ValueTask<MemoryTurnSession> BeginAsync(MemoryContext context, string turnId, string runId,
        string input, DateTimeOffset startedAt, Action<MemoryTurnSession> captureSession, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(captureSession);
        var conversation = await store.ReadAsync(context, cancellationToken);
        var turns = await store.ReadTurnsAsync(context, cancellationToken);
        if (turns.Any(t => t.TurnId == turnId || t.Status == MemoryTurnStatus.Running))
            throw new MemoryStoreException(MemoryStoreError.TurnConflict);
        var turn = new MemoryTurn(turnId, runId, conversation.Version, startedAt);
        var session = new MemoryTurnSession(store, context, turn);
        captureSession(session);
        await session.StartAsync(input, cancellationToken);
        return session;
    }
}
