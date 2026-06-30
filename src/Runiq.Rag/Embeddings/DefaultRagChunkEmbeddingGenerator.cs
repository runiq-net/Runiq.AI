using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;

namespace Runiq.Rag.Embeddings;

/// <summary>
/// Coordinates ordered chunk embedding generation using provider-neutral inputs and a text embedding provider.
/// </summary>
public sealed class DefaultRagChunkEmbeddingGenerator : IRagChunkEmbeddingGenerator
{
    private readonly IRagEmbeddingProvider embeddingProvider;
    private readonly IRagEmbeddingInputPreparer inputPreparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRagChunkEmbeddingGenerator"/> class.
    /// </summary>
    /// <param name="embeddingProvider">The text embedding provider used to generate each chunk embedding.</param>
    /// <param name="inputPreparer">The provider-neutral input preparer used before provider calls.</param>
    public DefaultRagChunkEmbeddingGenerator(
        IRagEmbeddingProvider embeddingProvider,
        IRagEmbeddingInputPreparer inputPreparer)
    {
        this.embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        this.inputPreparer = inputPreparer ?? throw new ArgumentNullException(nameof(inputPreparer));
    }

    /// <summary>
    /// Generates embeddings for chunks sequentially and returns results in the same order as the input chunks.
    /// </summary>
    /// <param name="chunks">The ordered chunks to generate embeddings for.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The generated chunk embedding results in input order.</returns>
    public async Task<IReadOnlyList<RagChunkEmbeddingResult>> GenerateAsync(
        IReadOnlyList<RagChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        cancellationToken.ThrowIfCancellationRequested();

        if (chunks.Count == 0)
        {
            return Array.Empty<RagChunkEmbeddingResult>();
        }

        var results = new List<RagChunkEmbeddingResult>(chunks.Count);

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index] ?? throw new InvalidOperationException(
                $"Chunk embedding generation failed at input index {index} because the chunk is null.");

            try
            {
                var input = await inputPreparer.PrepareAsync(chunk, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The embedding input preparer returned null.");

                var embedding = await embeddingProvider.GenerateAsync(input.Content, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The embedding provider returned null.");

                results.Add(new RagChunkEmbeddingResult
                {
                    ChunkId = input.ChunkId,
                    DocumentId = input.DocumentId,
                    ChunkIndex = input.ChunkIndex,
                    Embedding = embedding,
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Chunk embedding generation failed for chunk '{chunk.Id}' in document '{chunk.DocumentId}' at input index {index}.",
                    exception);
            }
        }

        return results;
    }
}
