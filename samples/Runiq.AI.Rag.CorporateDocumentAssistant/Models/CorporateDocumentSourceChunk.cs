namespace Runiq.AI.Rag.CorporateDocumentAssistant.Models;

/// <summary>
/// Describes one retrieved source chunk shown with a demo answer.
/// </summary>
public sealed class CorporateDocumentSourceChunk
{
    /// <summary>
    /// Gets the retrieved vector record identifier.
    /// </summary>
    public required string ChunkId { get; init; }

    /// <summary>
    /// Gets the source document identifier.
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// Gets the source document title or file name.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Gets the zero-based chunk index within the source document.
    /// </summary>
    public required string ChunkIndex { get; init; }

    /// <summary>
    /// Gets the raw score reported by the vector store.
    /// </summary>
    public double RawScore { get; init; }

    /// <summary>
    /// Gets the normalized relevance in the inclusive range from zero to one, when available.
    /// </summary>
    public double? Relevance { get; init; }

    /// <summary>
    /// Gets the metric that defines the raw score semantics.
    /// </summary>
    public string? Metric { get; init; }

    /// <summary>
    /// Gets a short snippet from the retrieved chunk content.
    /// </summary>
    public required string Snippet { get; init; }
}

