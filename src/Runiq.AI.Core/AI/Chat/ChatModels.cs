using Runiq.AI.Core.Models;

namespace Runiq.AI.Core.AI.Chat;

/// <summary>
/// Describes a provider-neutral chat request.
/// </summary>
/// <param name="Model">The provider and model to invoke.</param>
/// <param name="Messages">The ordered message list sent to the model.</param>
/// <param name="ProviderEndpoint">The resolved provider endpoint, when the caller has already selected one.</param>
/// <param name="ApiKey">The optional provider API key. Implementations must not expose this value in exceptions.</param>
/// <param name="Tools">The optional tool definitions available to the model.</param>
/// <param name="ResponseFormat">The optional structured output format requested from the provider.</param>
/// <param name="Options">Provider-neutral options and safe provider-specific extensions.</param>
public sealed record ChatRequest(
    ModelReference Model,
    IReadOnlyList<ChatMessage> Messages,
    Uri? ProviderEndpoint = null,
    string? ApiKey = null,
    IReadOnlyList<ChatToolDefinition>? Tools = null,
    ChatResponseFormat? ResponseFormat = null,
    ChatRequestOptions? Options = null)
{
    /// <summary>
    /// Validates the request and returns the same request for fluent call sites.
    /// </summary>
    /// <returns>The current request after validation succeeds.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required reference values are null.</exception>
    /// <exception cref="ArgumentException">Thrown when the message list is empty or contains null items.</exception>
    public ChatRequest Validate()
    {
        ArgumentNullException.ThrowIfNull(Model);
        ArgumentNullException.ThrowIfNull(Messages);

        if (Messages.Count == 0)
        {
            throw new ArgumentException("Chat request must contain at least one message.", nameof(Messages));
        }

        if (Messages.Any(message => message is null))
        {
            throw new ArgumentException("Chat request messages cannot contain null items.", nameof(Messages));
        }

        return this;
    }
}

/// <summary>
/// Represents one provider-neutral chat message.
/// </summary>
/// <param name="Role">The semantic role of the message.</param>
/// <param name="Content">The text content. Tool messages may use JSON text.</param>
/// <param name="ToolCallId">The provider tool-call identifier for tool result messages.</param>
/// <param name="ToolCalls">Tool calls requested by an assistant message.</param>
public sealed record ChatMessage(
    ChatRole Role,
    string Content,
    string? ToolCallId = null,
    IReadOnlyList<ChatToolCall>? ToolCalls = null);

/// <summary>
/// Identifies the role of a chat message.
/// </summary>
public enum ChatRole
{
    /// <summary>
    /// Instructions that define model behavior for the conversation.
    /// </summary>
    System,

    /// <summary>
    /// User-authored input.
    /// </summary>
    User,

    /// <summary>
    /// Assistant-authored model output.
    /// </summary>
    Assistant,

    /// <summary>
    /// Tool output returned to the model.
    /// </summary>
    Tool
}

/// <summary>
/// Represents a completed provider-neutral chat response.
/// </summary>
/// <param name="Message">The assistant message returned by the provider.</param>
/// <param name="FinishReason">The provider-neutral completion reason.</param>
/// <param name="Usage">The token usage reported by the provider, when available.</param>
/// <param name="ProviderResponseId">The provider response identifier, when available.</param>
public sealed record ChatResponse(
    ChatMessage Message,
    ChatFinishReason FinishReason = ChatFinishReason.Unknown,
    ChatUsage? Usage = null,
    string? ProviderResponseId = null);

/// <summary>
/// Represents one provider-neutral streaming update.
/// </summary>
/// <param name="Kind">The kind of update represented by this item.</param>
/// <param name="ContentDelta">The assistant content delta, when present.</param>
/// <param name="ToolCall">The tool call or tool-call delta, when present.</param>
/// <param name="FinishReason">The finish reason on a completion update.</param>
/// <param name="Usage">The final usage metadata, when the provider reports it.</param>
/// <param name="ProviderResponseId">The provider response identifier, when available.</param>
public sealed record ChatStreamingUpdate(
    ChatStreamingUpdateKind Kind,
    string? ContentDelta = null,
    ChatToolCall? ToolCall = null,
    ChatFinishReason? FinishReason = null,
    ChatUsage? Usage = null,
    string? ProviderResponseId = null);

/// <summary>
/// Identifies the semantic kind of a streaming update.
/// </summary>
public enum ChatStreamingUpdateKind
{
    /// <summary>
    /// Assistant text content was produced.
    /// </summary>
    ContentDelta,

    /// <summary>
    /// A tool call or tool-call argument delta was produced.
    /// </summary>
    ToolCallDelta,

    /// <summary>
    /// The provider completed the stream.
    /// </summary>
    Completed,

    /// <summary>
    /// The provider reported usage metadata.
    /// </summary>
    Usage
}

/// <summary>
/// Identifies why a model stopped producing output.
/// </summary>
public enum ChatFinishReason
{
    /// <summary>
    /// The provider did not report a finish reason.
    /// </summary>
    Unknown,

    /// <summary>
    /// The model stopped naturally.
    /// </summary>
    Stop,

    /// <summary>
    /// The model reached the configured output length.
    /// </summary>
    Length,

    /// <summary>
    /// The model requested tool execution.
    /// </summary>
    ToolCalls,

    /// <summary>
    /// The provider rejected content for policy reasons.
    /// </summary>
    ContentFilter,

    /// <summary>
    /// The provider failed before a normal finish reason was available.
    /// </summary>
    Error
}

/// <summary>
/// Token usage reported by a chat provider.
/// </summary>
/// <param name="InputTokens">The number of input tokens, when available.</param>
/// <param name="OutputTokens">The number of output tokens, when available.</param>
/// <param name="TotalTokens">The total number of tokens, when available.</param>
public sealed record ChatUsage(
    int? InputTokens = null,
    int? OutputTokens = null,
    int? TotalTokens = null);

/// <summary>
/// Describes a callable model tool.
/// </summary>
/// <param name="Name">The provider-visible tool name.</param>
/// <param name="Description">The provider-visible tool description.</param>
/// <param name="ParametersJsonSchema">The JSON schema object describing tool parameters.</param>
public sealed record ChatToolDefinition(
    string Name,
    string Description,
    string ParametersJsonSchema);

/// <summary>
/// Represents a tool call requested by a model.
/// </summary>
/// <param name="Id">The provider tool-call identifier.</param>
/// <param name="Name">The requested tool name.</param>
/// <param name="ArgumentsJson">The requested tool arguments as JSON text.</param>
public sealed record ChatToolCall(
    string Id,
    string Name,
    string ArgumentsJson);

/// <summary>
/// Describes a structured output response format.
/// </summary>
/// <param name="Name">The schema name sent to the provider.</param>
/// <param name="JsonSchema">The JSON schema object sent to the provider.</param>
public sealed record ChatResponseFormat(
    string Name,
    string JsonSchema);

/// <summary>
/// Carries optional model invocation settings that are not agent lifecycle concepts.
/// </summary>
public sealed class ChatRequestOptions
{
    /// <summary>
    /// Gets or sets provider-specific reasoning effort when the selected provider supports it.
    /// </summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>
    /// Gets or sets provider-specific output verbosity when the selected provider supports it.
    /// </summary>
    public string? Verbosity { get; set; }

    /// <summary>
    /// Gets safe provider-specific extension values. Implementations should ignore unknown values.
    /// </summary>
    public IDictionary<string, object?> Extensions { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}
