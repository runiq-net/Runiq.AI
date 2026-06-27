namespace Runiq.Rag.Models.Metadata;

/// <summary>
/// Represents metadata that applies to a RAG source document.
/// </summary>
public sealed class RagDocumentMetadata
{
    private RagMetadata additionalMetadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagDocumentMetadata"/> class.
    /// </summary>
    public RagDocumentMetadata()
    {
    }

    /// <summary>
    /// Gets or initializes the source identifier for the document.
    /// </summary>
    public string? SourceId { get; init; }

    /// <summary>
    /// Gets or initializes the source display name for the document.
    /// </summary>
    public string? SourceName { get; init; }

    /// <summary>
    /// Gets or initializes the source URI for the document.
    /// </summary>
    public string? SourceUri { get; init; }

    /// <summary>
    /// Gets or initializes the content type for the document.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets or initializes the document creation timestamp.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the document update timestamp.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Gets or initializes additional document metadata.
    /// </summary>
    public RagMetadata AdditionalMetadata
    {
        get => additionalMetadata;
        init => additionalMetadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
