using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.Documents;

/// <summary>
/// Represents a source document before or after chunking.
/// </summary>
public sealed class RagDocument
{
    private RagDocumentMetadata metadata = new();
    private IList<RagChunk> chunks = new List<RagChunk>();

    /// <summary>
    /// Initializes a new instance of the <see cref="RagDocument"/> class.
    /// </summary>
    public RagDocument()
    {
    }

    /// <summary>
    /// Gets or initializes the document identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or initializes the document content.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the document metadata.
    /// </summary>
    public RagDocumentMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes the chunks extracted from the document.
    /// </summary>
    public IList<RagChunk> Chunks
    {
        get => chunks;
        init => chunks = value ?? throw new ArgumentNullException(nameof(value));
    }
}
