using System.Text.Json;

namespace Runiq.AI.Core.AI.Chat;

/// <summary>Owns opaque provider output needed to continue the current tool turn without server-side history.</summary>
/// <remarks>This replaces the associated assistant message's wire representation, not its tool execution contract.
/// Keep it invocation-local; it is not user-visible content or a durable conversation transcript.</remarks>
public sealed class ChatContinuation
{
    /// <summary>Snapshots provider output independently of its source JSON document.</summary>
    /// <param name="format">The provider-defined representation identifier.</param>
    /// <param name="payload">The complete provider output representation for one model round.</param>
    /// <param name="outputTokens">Reported output tokens, including hidden reasoning, or null when unavailable.</param>
    /// <exception cref="ArgumentException">The format is blank or the payload is undefined.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The reported token count is negative.</exception>
    public ChatContinuation(string format, JsonElement payload, int? outputTokens = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        if (payload.ValueKind == JsonValueKind.Undefined) throw new ArgumentException("A defined payload is required.", nameof(payload));
        if (outputTokens < 0) throw new ArgumentOutOfRangeException(nameof(outputTokens));
        Format = format;
        Payload = payload.Clone();
        OutputTokens = outputTokens;
    }

    /// <summary>Gets the provider-defined representation identifier.</summary>
    public string Format { get; }
    /// <summary>Gets the owned output payload; orchestration must preserve it without interpreting provider fields.</summary>
    public JsonElement Payload { get; }
    /// <summary>Gets reported output tokens, including reasoning, used as a floor for continuation accounting.</summary>
    public int? OutputTokens { get; }
}
