using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Embeddings;

/// <summary>
/// Prepares provider-neutral embedding inputs from RAG chunks.
/// </summary>
public sealed class DefaultRagEmbeddingInputPreparer : IRagEmbeddingInputPreparer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRagEmbeddingInputPreparer"/> class.
    /// </summary>
    public DefaultRagEmbeddingInputPreparer()
    {
    }

    /// <summary>
    /// Creates an embedding input by preserving chunk identity, document identity, content, order, and metadata.
    /// </summary>
    /// <param name="chunk">The source chunk to prepare for embedding generation.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The provider-neutral embedding input created from the chunk.</returns>
    public Task<RagEmbeddingInput> PrepareAsync(
        RagChunk chunk,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        cancellationToken.ThrowIfCancellationRequested();

        var input = new RagEmbeddingInput
        {
            Id = chunk.Id,
            ChunkId = chunk.Id,
            DocumentId = chunk.DocumentId,
            Content = chunk.Content,
            ChunkIndex = chunk.Index,
            Metadata = CopyMetadata(chunk.Metadata),
        };

        return Task.FromResult(input);
    }

    private static RagChunkMetadata CopyMetadata(RagChunkMetadata metadata)
    {
        return new RagChunkMetadata
        {
            StartIndex = metadata.StartIndex,
            EndIndex = metadata.EndIndex,
            TokenCount = metadata.TokenCount,
            AdditionalMetadata = new RagMetadata(metadata.AdditionalMetadata.Values),
        };
    }
}
