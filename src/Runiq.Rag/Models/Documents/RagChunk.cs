using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.Documents;

/// <summary>
/// Represents a chunk extracted from a source document.
/// </summary>
public sealed class RagChunk
{
    private RagChunkMetadata metadata = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RagChunk"/> class.
    /// </summary>
    public RagChunk()
    {
    }

    /// <summary>
    /// Gets or initializes the chunk identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or initializes the source document identifier.
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// Gets or initializes the chunk content.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the chunk order inside the source document.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets or initializes the chunk metadata.
    /// </summary>
    public RagChunkMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
