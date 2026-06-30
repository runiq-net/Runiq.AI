using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.Documents;

/// <summary>
/// Represents a source document used as the first input of the RAG ingestion pipeline.
/// </summary>
public sealed class RagDocument
{
    private string id = string.Empty;
    private string content = string.Empty;
    private RagDocumentMetadata metadata = new();
    private IList<RagChunk> chunks = new List<RagChunk>();

    /// <summary>
    /// Initializes a new instance of the <see cref="RagDocument"/> class.
    /// </summary>
    public RagDocument()
    {
    }

    /// <summary>
    /// Gets or initializes the stable document identifier used to correlate chunks and embeddings with the source document.
    /// </summary>
    public required string Id
    {
        get => id;
        init => id = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Document id cannot be null, empty, or whitespace.", nameof(value))
            : value;
    }

    /// <summary>
    /// Gets or initializes the primary text content used by chunking.
    /// </summary>
    public string Content
    {
        get => content;
        init => content = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes source metadata carried with the document during ingestion.
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
