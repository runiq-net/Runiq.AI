using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;

namespace Runiq.AI.Rag.Abstractions.Embeddings;

/// <summary>
/// Defines a provider-neutral service that prepares chunk content for embedding generation.
/// </summary>
public interface IRagEmbeddingInputPreparer
{
    /// <summary>
    /// Creates an embedding input from a source chunk without calling an embedding provider.
    /// </summary>
    /// <param name="chunk">The source chunk to prepare for embedding generation.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The provider-neutral embedding input created from the chunk.</returns>
    Task<RagEmbeddingInput> PrepareAsync(
        RagChunk chunk,
        CancellationToken cancellationToken = default);
}

