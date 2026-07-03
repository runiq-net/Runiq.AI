using System.Text;
using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Models.Embeddings;

namespace Runiq.Rag.Tests.Retrieval.Integration.Support;

/// <summary>
/// A deterministic, provider-independent embedding test double used by the retrieval integration tests. It
/// projects text onto a fixed keyword vocabulary by counting how often each vocabulary term appears, so the
/// same text always yields the same vector without any network, database, SDK, or real embedding provider.
/// Because query text and stored chunk content are embedded through the identical vocabulary, cosine
/// similarity ranks a record by how much its content overlaps the query, which makes similarity ordering
/// provable in tests. It is intentionally small and transparent rather than a general-purpose embedding.
/// </summary>
public sealed class DeterministicKeywordEmbeddingProvider : IRagEmbeddingProvider
{
    /// <summary>
    /// The ordered keyword vocabulary. Each entry maps to one embedding dimension, and the tests craft their
    /// content and query text from these words so that keyword overlap drives the similarity ordering.
    /// </summary>
    private static readonly string[] Vocabulary =
    [
        "database",
        "index",
        "tuning",
        "performance",
        "backup",
        "restore",
        "schedule",
        "network",
        "latency",
        "security",
        "cooking",
        "recipe",
        "travel",
        "guide",
    ];

    /// <summary>
    /// Gets the fixed number of dimensions produced by this embedding, equal to the vocabulary size. Vector
    /// indexes seeded for retrieval tests must be created with this dimension count.
    /// </summary>
    public int Dimensions => Vocabulary.Length;

    /// <summary>
    /// Generates a deterministic embedding for the supplied text through the keyword vocabulary, honoring
    /// cancellation but never performing any external call.
    /// </summary>
    /// <param name="text">The query text or chunk content to embed.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The deterministic embedding for the text.</returns>
    public Task<RagEmbedding> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new RagEmbedding(Embed(text)));
    }

    /// <summary>
    /// Computes the deterministic vector values for the supplied text by counting vocabulary term occurrences.
    /// Exposed so test setup can embed chunk content into the same vector space the retrieval pipeline uses to
    /// embed query text.
    /// </summary>
    /// <param name="text">The text to convert into deterministic vector values.</param>
    /// <returns>The deterministic vector values with <see cref="Dimensions"/> dimensions.</returns>
    public IReadOnlyList<float> Embed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var counts = new float[Vocabulary.Length];

        foreach (var token in Tokenize(text))
        {
            var index = Array.IndexOf(Vocabulary, token);
            if (index >= 0)
            {
                counts[index] += 1f;
            }
        }

        return counts;
    }

    /// <summary>
    /// Splits text into lowercase alphabetic tokens, treating every non-letter character as a separator, so
    /// punctuation and casing never affect the resulting vector.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <returns>The lowercase word tokens found in the text.</returns>
    private static IEnumerable<string> Tokenize(string text)
    {
        var token = new StringBuilder();

        foreach (var character in text)
        {
            if (char.IsLetter(character))
            {
                token.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (token.Length > 0)
            {
                yield return token.ToString();
                token.Clear();
            }
        }

        if (token.Length > 0)
        {
            yield return token.ToString();
        }
    }
}
