using Runiq.AI.Rag.Models.Documents;

namespace Runiq.AI.Rag.Models.Ingestion;

/// <summary>
/// Represents the provider-neutral output of ingesting a RAG document into chunks and embeddings.
/// </summary>
public sealed class RagDocumentIngestionResult
{
    private string documentId = string.Empty;
    private IReadOnlyList<RagChunk> chunks = Array.Empty<RagChunk>();
    private IReadOnlyList<RagDocumentIngestionItem> items = Array.Empty<RagDocumentIngestionItem>();

    /// <summary>
    /// Gets or initializes the identifier of the ingested source document.
    /// </summary>
    public required string DocumentId
    {
        get => documentId;
        init => documentId = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Document ingestion result document id cannot be null, empty, or whitespace.", nameof(value))
            : value;
    }

    /// <summary>
    /// Gets or initializes the ordered chunks produced by the chunker.
    /// </summary>
    public IReadOnlyList<RagChunk> Chunks
    {
        get => chunks;
        init => chunks = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes the ordered chunk and embedding associations produced during ingestion.
    /// </summary>
    public IReadOnlyList<RagDocumentIngestionItem> Items
    {
        get => items;
        init => items = value ?? throw new ArgumentNullException(nameof(value));
    }
}

