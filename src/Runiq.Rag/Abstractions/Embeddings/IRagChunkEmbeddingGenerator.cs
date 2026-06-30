using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;

namespace Runiq.Rag.Abstractions.Embeddings;

/// <summary>
/// Defines a provider-neutral service that generates embeddings for an ordered list of RAG chunks.
/// </summary>
public interface IRagChunkEmbeddingGenerator
{
    /// <summary>
    /// Generates embeddings for the specified chunks while preserving source chunk and document identity.
    /// </summary>
    /// <param name="chunks">The ordered chunks to generate embeddings for.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The generated chunk embedding results in the same order as the input chunks.</returns>
    Task<IReadOnlyList<RagChunkEmbeddingResult>> GenerateAsync(
        IReadOnlyList<RagChunk> chunks,
        CancellationToken cancellationToken = default);
}
