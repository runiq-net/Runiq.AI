using Runiq.AI.Rag.Models.Embeddings;

namespace Runiq.AI.Rag.Abstractions.Embeddings;

/// <summary>
/// Defines a provider that generates vector embeddings from text content for RAG operations.
/// </summary>
public interface IRagEmbeddingProvider
{
    /// <summary>
    /// Generates a vector embedding for the specified text.
    /// </summary>
    /// <param name="text">The text content to embed.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The generated embedding.</returns>
    Task<RagEmbedding> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default);
}

