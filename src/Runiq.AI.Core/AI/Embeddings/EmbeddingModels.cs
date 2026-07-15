using Runiq.AI.Core.Models;

namespace Runiq.AI.Core.AI.Embeddings;

/// <summary>
/// Invokes provider-neutral text embedding models.
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>
    /// Embeds one or more text inputs and preserves input order in the returned results.
    /// </summary>
    /// <param name="request">The embedding request to execute.</param>
    /// <param name="cancellationToken">Cancels the provider request before all embeddings are returned.</param>
    /// <returns>An embedding response whose result order matches the request input order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the request is structurally invalid.</exception>
    Task<EmbeddingResponse> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a provider-neutral embedding request.
/// </summary>
/// <param name="Model">The provider and model to invoke.</param>
/// <param name="Inputs">The ordered text inputs to embed.</param>
/// <param name="ProviderEndpoint">The resolved provider endpoint, when the caller has already selected one.</param>
/// <param name="ApiKey">The optional provider API key. Implementations must not expose this value in exceptions.</param>
/// <param name="Dimensions">The requested vector dimension count, when the provider supports it.</param>
public sealed record EmbeddingRequest(
    ModelReference Model,
    IReadOnlyList<string> Inputs,
    Uri? ProviderEndpoint = null,
    string? ApiKey = null,
    int? Dimensions = null)
{
    /// <summary>
    /// Validates the request and returns the same request for fluent call sites.
    /// </summary>
    /// <returns>The current request after validation succeeds.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required reference values are null.</exception>
    /// <exception cref="ArgumentException">Thrown when inputs are empty or contain null values.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Dimensions"/> is less than one.</exception>
    public EmbeddingRequest Validate()
    {
        ArgumentNullException.ThrowIfNull(Model);
        ArgumentNullException.ThrowIfNull(Inputs);

        if (Inputs.Count == 0)
        {
            throw new ArgumentException("Embedding request must contain at least one input.", nameof(Inputs));
        }

        if (Inputs.Any(input => input is null))
        {
            throw new ArgumentException("Embedding request inputs cannot contain null items.", nameof(Inputs));
        }

        if (Dimensions is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Dimensions), "Embedding dimensions must be greater than zero.");
        }

        return this;
    }
}

/// <summary>
/// Represents provider-neutral embedding results.
/// </summary>
/// <param name="Results">The embedding results in the same order as request inputs.</param>
/// <param name="Usage">The token usage reported by the provider, when available.</param>
public sealed record EmbeddingResponse(
    IReadOnlyList<EmbeddingResult> Results,
    EmbeddingUsage? Usage = null);

/// <summary>
/// Represents one embedding result.
/// </summary>
/// <param name="Index">The zero-based index of the source input.</param>
/// <param name="Vector">The embedding vector values.</param>
/// <param name="Dimensions">The actual vector dimension count.</param>
public sealed record EmbeddingResult(
    int Index,
    IReadOnlyList<float> Vector,
    int Dimensions);

/// <summary>
/// Token usage reported by an embedding provider.
/// </summary>
/// <param name="InputTokens">The number of input tokens, when available.</param>
/// <param name="TotalTokens">The total number of tokens, when available.</param>
public sealed record EmbeddingUsage(
    int? InputTokens = null,
    int? TotalTokens = null);
