using Runiq.AI.Rag.Models.Metadata;

namespace Runiq.AI.Rag.Models.Documents;

/// <summary>
/// Represents a chunk extracted from a source document.
/// </summary>
public sealed class RagChunk
{
    private string id = string.Empty;
    private string documentId = string.Empty;
    private string content = string.Empty;
    private RagChunkMetadata metadata = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RagChunk"/> class.
    /// </summary>
    public RagChunk()
    {
    }

    /// <summary>
    /// Gets or initializes the stable chunk identifier used to correlate embeddings and vector records with this chunk.
    /// </summary>
    public required string Id
    {
        get => id;
        init => id = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Chunk id cannot be null, empty, or whitespace.", nameof(value))
            : value;
    }

    /// <summary>
    /// Gets or initializes the source document identifier that this chunk was extracted from.
    /// </summary>
    public required string DocumentId
    {
        get => documentId;
        init => documentId = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Chunk document id cannot be null, empty, or whitespace.", nameof(value))
            : value;
    }

    /// <summary>
    /// Gets or initializes the chunk text that is intended to be used as embedding input.
    /// </summary>
    public string Content
    {
        get => content;
        init => content = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes the zero-based chunk order inside the source document.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets or initializes provider-neutral metadata that describes the chunk boundaries, token count, and custom values.
    /// </summary>
    public RagChunkMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}

