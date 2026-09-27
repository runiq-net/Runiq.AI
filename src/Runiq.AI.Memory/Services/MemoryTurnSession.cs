using System.Text;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Models;

namespace Runiq.AI.Memory.Services;

/// <summary>Maintains sequential writes for one reserved turn; callers must not use a session concurrently.</summary>
/// <remarks>Each append retains its original payload through an uncertain commit. Unsuccessful turns are diagnostic only.</remarks>
public sealed class MemoryTurnSession
{
    private readonly IMemoryConversationStore store;
    private readonly MemoryContext context;
    private readonly StringBuilder assistant = new();
    private MemoryAppendRequest? pending;
    private long version;
    private readonly string writePrefix = Guid.NewGuid().ToString("N");
    private int appendIndex;
    private int pendingAssistantCharacters;

    internal MemoryTurnSession(IMemoryConversationStore store, MemoryContext context, MemoryTurn turn)
    {
        this.store = store;
        this.context = context;
        Turn = turn;
        version = turn.HistoryVersion;
    }

    /// <summary>Gets the lifecycle snapshot; the initial Running proposal is not confirmed until the first append receipt is received.</summary>
    public MemoryTurn Turn { get; private set; }

    internal ValueTask StartAsync(string input, CancellationToken cancellationToken) =>
        WriteAsync([new(ChatRole.User, input)], Turn, cancellationToken);

    /// <summary>Loads every page up to the reservation boundary, excluding all failed, cancelled, or unfinished turns.</summary>
    /// <param name="cancellationToken">Cancels content reads and repeated authorization checks.</param>
    /// <returns>Ordered Core messages without the current user input or transient context.</returns>
    /// <remarks>Legacy messages without a durable completed turn are excluded because their outcome is unknown.</remarks>
    public async ValueTask<IReadOnlyList<ChatMessage>> LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        var completed = (await store.ReadTurnsAsync(context, cancellationToken))
            .Where(t => t.Status == MemoryTurnStatus.Completed).Select(t => t.RunId).ToHashSet(StringComparer.Ordinal);
        var history = new List<ChatMessage>();
        long cursor = 0;
        while (cursor < Turn.HistoryVersion)
        {
            var page = await store.ReadMessagesAsync(context, cursor, 100, cancellationToken);
            if (page.Count == 0) throw new MemoryStoreException(MemoryStoreError.InvalidPayload);
            foreach (var message in page)
            {
                if (message.Sequence > Turn.HistoryVersion) break;
                if (completed.Contains(message.Content.RunId)) history.Add(message.Content.Message);
            }
            cursor = page[^1].Sequence;
        }
        return history.AsReadOnly();
    }

    /// <summary>Accumulates a streaming text fragment without creating a completed message per chunk.</summary>
    /// <param name="content">The assistant text fragment received from the executor.</param>
    public void AppendDelta(string content) => assistant.Append(content);

    /// <summary>Persists one assistant tool-call message, including text emitted in that model round, before tool execution.</summary>
    /// <param name="calls">The complete ordered calls with their original identifiers and exact arguments.</param>
    /// <param name="cancellationToken">Cancels the append before commit.</param>
    /// <returns>A task completed after the tool-call message is durable.</returns>
    public ValueTask AppendToolCallsAsync(IReadOnlyList<ChatToolCall> calls, CancellationToken cancellationToken = default) =>
        WriteAsync([new(ChatRole.Assistant, assistant.ToString(), ToolCalls: calls)], Turn, cancellationToken, assistant.Length);

    /// <summary>Persists the exact success or failure payload that will be supplied to the model.</summary>
    /// <param name="callId">The identifier of a previously persisted call in this invocation.</param>
    /// <param name="output">The exact model-facing result payload.</param>
    /// <param name="cancellationToken">Cancels the append before commit.</param>
    /// <returns>A task completed after the tool result is durable.</returns>
    public ValueTask AppendToolResultAsync(string callId, string output, CancellationToken cancellationToken = default) =>
        WriteAsync([new(ChatRole.Tool, output, callId)], Turn, cancellationToken);

    /// <summary>Commits accumulated assistant output and the terminal state atomically.</summary>
    /// <param name="status">The completed, failed, or cancelled outcome.</param>
    /// <param name="cancellationToken">Cancels the write; cleanup callers may supply a bounded independent token.</param>
    /// <returns>A task completed after the terminal receipt is confirmed, or without writes if the initial reservation was authoritatively rejected.</returns>
    public async ValueTask FinishAsync(MemoryTurnStatus status, CancellationToken cancellationToken = default)
    {
        if (status == MemoryTurnStatus.Running) throw new ArgumentException("A terminal outcome is required.", nameof(status));
        if (Turn.Status != MemoryTurnStatus.Running) return;
        // A captured session whose reservation was authoritatively rejected owns no durable turn to finalize.
        if (pending is null && version == Turn.HistoryVersion) return;
        // Resolve an uncertain prior write before creating a different payload or terminal transition.
        if (pending is not null) await CommitPendingAsync(cancellationToken);
        if (Turn.Status != MemoryTurnStatus.Running) return;
        var terminal = new MemoryTurn(Turn.TurnId, Turn.RunId, Turn.HistoryVersion, Turn.StartedAt, status, DateTimeOffset.UtcNow);
        await WriteAsync(assistant.Length == 0 ? [] : [new(ChatRole.Assistant, assistant.ToString())], terminal, cancellationToken, assistant.Length);
    }

    private async ValueTask WriteAsync(IReadOnlyList<ChatMessage> messages, MemoryTurn turn, CancellationToken cancellationToken, int assistantCharacters = 0)
    {
        if (pending is not null) throw new InvalidOperationException("An uncertain append must be resolved before writing new content.");
        var key = $"{writePrefix}:{appendIndex++}";
        pending = new MemoryAppendRequest(key, version, messages.Select((m, i) =>
            new MemoryMessage($"{key}:{i}", Turn.RunId, DateTimeOffset.UtcNow, m)).ToArray(), turn);
        pendingAssistantCharacters = assistantCharacters;
        try { await CommitPendingAsync(cancellationToken); }
        catch (MemoryStoreException exception) when (exception.Error != MemoryStoreError.StorageFailure)
        {
            // Authoritative rejections are known not to commit; finalization may record the failed outcome.
            pending = null;
            pendingAssistantCharacters = 0;
            throw;
        }
    }

    private async ValueTask CommitPendingAsync(CancellationToken cancellationToken)
    {
        var request = pending!;
        MemoryAppendResult result;
        try { result = await store.AppendAsync(context, request, cancellationToken); }
        catch (MemoryStoreException exception) when (exception.Error == MemoryStoreError.StorageFailure && !cancellationToken.IsCancellationRequested)
        {
            // Retry only the immutable write, once. Never regenerate timestamps, keys, versions, or tool work.
            result = await store.AppendAsync(context, request, cancellationToken);
        }
        version = result.Version;
        Turn = request.Turn!;
        assistant.Remove(0, pendingAssistantCharacters);
        pendingAssistantCharacters = 0;
        pending = null;
    }
}
