using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.Embeddings;

/// <summary>
/// Represents provider-neutral text and source metadata prepared for embedding generation.
/// </summary>
public sealed class RagEmbeddingInput
{
    private string id = string.Empty;
    private string chunkId = string.Empty;
    private string documentId = string.Empty;
    private string content = string.Empty;
    private RagChunkMetadata metadata = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RagEmbeddingInput"/> class.
    /// </summary>
    public RagEmbeddingInput()
    {
    }

    /// <summary>
    /// Gets or initializes the stable embedding input identifier used to correlate provider calls with source chunks.
    /// </summary>
    public required string Id
    {
        get => id;
        init => id = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Embedding input id cannot be null, empty, or whitespace.", nameof(value))
            : value;
    }

    /// <summary>
    /// Gets or initializes the source chunk identifier that produced this embedding input.
    /// </summary>
    public required string ChunkId
    {
        get => chunkId;
        init => chunkId = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Embedding input chunk id cannot be null, empty, or whitespace.", nameof(value))
            : value;
    }

    /// <summary>
    /// Gets or initializes the source document identifier that produced the source chunk.
    /// </summary>
    public required string DocumentId
    {
        get => documentId;
        init => documentId = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Embedding input document id cannot be null, empty, or whitespace.", nameof(value))
            : value;
    }

    /// <summary>
    /// Gets or initializes the text content to send to an embedding provider.
    /// </summary>
    public string Content
    {
        get => content;
        init => content = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes the zero-based source chunk order inside the source document.
    /// </summary>
    public int ChunkIndex { get; init; }

    /// <summary>
    /// Gets or initializes provider-neutral metadata copied from the source chunk.
    /// </summary>
    public RagChunkMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
