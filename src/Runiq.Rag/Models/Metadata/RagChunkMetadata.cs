namespace Runiq.Rag.Models.Metadata;

/// <summary>
/// Represents metadata that applies to a RAG document chunk.
/// </summary>
public sealed class RagChunkMetadata
{
    private RagMetadata additionalMetadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagChunkMetadata"/> class.
    /// </summary>
    public RagChunkMetadata()
    {
    }

    /// <summary>
    /// Gets or initializes the starting character index of the chunk in the source document.
    /// </summary>
    public int? StartIndex { get; init; }

    /// <summary>
    /// Gets or initializes the ending character index of the chunk in the source document.
    /// </summary>
    public int? EndIndex { get; init; }

    /// <summary>
    /// Gets or initializes the estimated token count for the chunk.
    /// </summary>
    public int? TokenCount { get; init; }

    /// <summary>
    /// Gets or initializes additional chunk metadata.
    /// </summary>
    public RagMetadata AdditionalMetadata
    {
        get => additionalMetadata;
        init => additionalMetadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
