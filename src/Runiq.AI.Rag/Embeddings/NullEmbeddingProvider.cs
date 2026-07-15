using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Models.Embeddings;

namespace Runiq.AI.Rag.Embeddings;

/// <summary>
/// Provides a safe no-op embedding provider that returns empty embeddings.
/// </summary>
public sealed class NullEmbeddingProvider : IRagEmbeddingProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullEmbeddingProvider"/> class.
    /// </summary>
    public NullEmbeddingProvider()
    {
    }

    /// <summary>
    /// Generates an empty embedding without calling external services.
    /// </summary>
    /// <param name="text">The text content to embed.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>An empty RAG embedding.</returns>
    public Task<RagEmbedding> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new RagEmbedding());
    }
}

