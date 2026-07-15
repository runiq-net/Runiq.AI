using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Models.Embeddings;

namespace Runiq.AI.Rag.CorporateDocumentAssistant.Services;

/// <summary>
/// Generates deterministic sample embeddings so the application can run without external provider credentials.
/// </summary>
public sealed class DeterministicCorporateEmbeddingProvider : IRagEmbeddingProvider
{
    /// <summary>
    /// Gets the fixed vector dimension count produced by the sample provider.
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
    /// Creates a stable local embedding vector from text content for repeatable sample output and tests.
    /// </summary>
    /// <param name="text">The text to convert into a deterministic demo embedding.</param>
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

