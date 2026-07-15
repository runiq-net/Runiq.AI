namespace Runiq.AI.Rag.Models.Embeddings;

/// <summary>
/// Represents an embedding generated for a source chunk and its document.
/// </summary>
public sealed class RagChunkEmbeddingResult
{
    private string chunkId = string.Empty;
    private string documentId = string.Empty;
    private RagEmbedding embedding = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RagChunkEmbeddingResult"/> class.
    /// </summary>
    public RagChunkEmbeddingResult()
    {
    }

    /// <summary>
    /// Gets or initializes the identifier of the chunk that produced the embedding.
    /// </summary>
    public required string ChunkId
    {
        get => chunkId;
        init => chunkId = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Chunk embedding result chunk id cannot be null, empty, or whitespace.", nameof(value))
            : value;
    }

    /// <summary>
    /// Gets or initializes the identifier of the document that owns the source chunk.
    /// </summary>
    public required string DocumentId
    {
        get => documentId;
        init => documentId = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Chunk embedding result document id cannot be null, empty, or whitespace.", nameof(value))
            : value;
    }

    /// <summary>
    /// Gets or initializes the zero-based source chunk order inside the source document.
    /// </summary>
    public int ChunkIndex { get; init; }

    /// <summary>
    /// Gets or initializes the vector embedding generated for the source chunk.
    /// </summary>
    public required RagEmbedding Embedding
    {
        get => embedding;
        init => embedding = value ?? throw new ArgumentNullException(nameof(value));
    }
}

