using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;

namespace Runiq.Rag.Models.Ingestion;

/// <summary>
/// Represents a generated embedding result associated with the exact source chunk that produced it.
/// </summary>
public sealed class RagDocumentIngestionItem
{
    private RagChunk chunk = null!;
    private RagChunkEmbeddingResult embeddingResult = null!;

    /// <summary>
    /// Gets or initializes the source chunk that was embedded.
    /// </summary>
    public required RagChunk Chunk
    {
        get => chunk;
        init => chunk = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes the embedding result generated for the source chunk.
    /// </summary>
    public required RagChunkEmbeddingResult EmbeddingResult
    {
        get => embeddingResult;
        init => embeddingResult = value ?? throw new ArgumentNullException(nameof(value));
    }
}
