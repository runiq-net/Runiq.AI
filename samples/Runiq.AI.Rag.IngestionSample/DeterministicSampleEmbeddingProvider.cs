using Runiq.AI.Core.AI.Embeddings;

namespace Runiq.AI.Rag.IngestionSample;

/// <summary>
/// Generates small deterministic vectors so the sample can run without API keys, network calls, or provider-specific setup.
/// </summary>
public sealed class DeterministicSampleEmbeddingProvider : IEmbeddingClient
{
    /// <summary>
    /// Gets the fixed number of vector dimensions produced by the sample provider.
    /// </summary>
    public const int Dimensions = 8;

    /// <inheritdoc />
    public Task<EmbeddingResponse> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new EmbeddingResponse(request.Inputs.Select((text, index) => new EmbeddingResult(index, CreateEmbeddingValues(text), Dimensions)).ToList()));
    }

    /// <summary>
    /// Creates a repeatable vector from text content using only local deterministic operations.
    /// </summary>
    /// <param name="text">The text that should be represented as a sample embedding.</param>
    /// <returns>A deterministic vector with <see cref="Dimensions" /> values.</returns>
    public static IReadOnlyList<float> CreateEmbeddingValues(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var values = new float[Dimensions];

        for (var index = 0; index < text.Length; index++)
        {
            var dimension = index % values.Length;
            values[dimension] += ((text[index] * (index + 1)) % 997) / 997.0f;
        }

        for (var index = 0; index < values.Length; index++)
        {
            values[index] = MathF.Round(values[index], 6);
        }

        return values;
    }
}

