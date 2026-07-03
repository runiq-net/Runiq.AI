using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Models.Embeddings;

namespace Runiq.Rag.UpsertPipelineSample;

/// <summary>
/// Generates small deterministic vectors so the upsert pipeline sample can run without API keys,
/// network calls, or provider-specific setup. The provider exists only inside this sample and is
/// not intended for production embedding generation.
/// </summary>
public sealed class DeterministicSampleEmbeddingProvider : IRagEmbeddingProvider
{
    /// <summary>
    /// Gets the fixed number of vector dimensions produced by the sample provider. The sample
    /// creates its vector index with this dimension count so upserted records always pass the
    /// dimension validation applied by the upsert pipeline.
    /// </summary>
    public const int Dimensions = 8;

    /// <inheritdoc />
    public Task<RagEmbedding> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new RagEmbedding(CreateEmbeddingValues(text)));
    }

    /// <summary>
    /// Creates a repeatable vector from text content using only local deterministic operations,
    /// so the same chunk content always maps to the same stored vector.
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
