using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Validation;

namespace Runiq.AI.Memory.Models;

/// <summary>Contains neutral message data supplied to an atomic append.</summary>
public sealed record MemoryMessage
{
    /// <summary>Creates a message with a stable identity and invocation association.</summary>
    /// <param name="messageId">The identifier unique within the tenant/thread.</param>
    /// <param name="runId">The invocation identifier; never an authorization capability.</param>
    /// <param name="timestamp">The original message time, preserved including offset and ticks.</param>
    /// <param name="message">The Core message, including tool relationships but excluding invocation-local continuation state.</param>
    /// <exception cref="ArgumentException">Identifiers or message fields are invalid.</exception>
    /// <exception cref="ArgumentNullException">The message is null.</exception>
    public MemoryMessage(string messageId, string runId, DateTimeOffset timestamp, ChatMessage message)
    {
        MessageId = MemoryIdentifier.Validate(messageId, nameof(messageId));
        RunId = MemoryIdentifier.Validate(runId, nameof(runId));
        ArgumentNullException.ThrowIfNull(message);
        MessageValidation.Validate(message);
        Timestamp = timestamp;
        // Durable snapshots must not retain provider state, including in stores that keep objects in memory.
        Message = message with { Continuation = null, ToolCalls = message.ToolCalls is null ? null : Array.AsReadOnly(message.ToolCalls.ToArray()) };
    }

    /// <summary>Gets the stable message identifier.</summary>
    public string MessageId { get; }
    /// <summary>Gets the neutral invocation identifier.</summary>
    public string RunId { get; }
    /// <summary>Gets the original timestamp, independent of sequence assignment.</summary>
    public DateTimeOffset Timestamp { get; }
    /// <summary>Gets the immutable Core message snapshot.</summary>
    public ChatMessage Message { get; }
}
