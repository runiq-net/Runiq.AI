namespace Runiq.AI.Core.AI.Chat;

/// <summary>
/// Identifies provider-neutral model invocation error categories.
/// </summary>
public enum ProviderErrorKind
{
    /// <summary>
    /// The provider returned an error that could not be classified.
    /// </summary>
    Unknown,

    /// <summary>
    /// Authentication or authorization failed.
    /// </summary>
    AuthenticationFailure,

    /// <summary>
    /// The request payload or configuration was invalid.
    /// </summary>
    InvalidRequest,

    /// <summary>
    /// The provider rejected the request because of rate limits.
    /// </summary>
    RateLimited,

    /// <summary>
    /// The provider request timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// The provider service was unavailable.
    /// </summary>
    ProviderUnavailable,

    /// <summary>
    /// The selected model was not found.
    /// </summary>
    ModelNotFound,

    /// <summary>
    /// The selected provider or model does not support the requested operation.
    /// </summary>
    UnsupportedOperation,

    /// <summary>
    /// The provider rejected the content for policy reasons.
    /// </summary>
    ContentPolicyRejected,

    /// <summary>
    /// The provider returned malformed or incomplete data.
    /// </summary>
    MalformedProviderResponse
}

/// <summary>
/// Exception thrown when a model provider request fails after safe provider-neutral mapping.
/// </summary>
public sealed class ChatProviderException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatProviderException"/> class.
    /// </summary>
    /// <param name="kind">The provider-neutral error kind.</param>
    /// <param name="message">A safe message that must not include secrets.</param>
    /// <param name="providerName">The provider name associated with the failure, when available.</param>
    /// <param name="statusCode">The HTTP status code associated with the failure, when available.</param>
    /// <param name="innerException">The original exception, when preserving diagnostics is useful.</param>
    public ChatProviderException(
        ProviderErrorKind kind,
        string message,
        string? providerName = null,
        int? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        ProviderName = providerName;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the provider-neutral error kind.
    /// </summary>
    public ProviderErrorKind Kind { get; }

    /// <summary>
    /// Gets the provider name associated with the failure, when available.
    /// </summary>
    public string? ProviderName { get; }

    /// <summary>
    /// Gets the HTTP status code associated with the failure, when available.
    /// </summary>
    public int? StatusCode { get; }
}
