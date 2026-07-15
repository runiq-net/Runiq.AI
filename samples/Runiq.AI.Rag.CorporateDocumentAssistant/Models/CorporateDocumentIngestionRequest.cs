namespace Runiq.AI.Rag.CorporateDocumentAssistant.Models;

/// <summary>
/// Represents plain-text document content submitted to the corporate document assistant sample for ingestion.
/// </summary>
public sealed class CorporateDocumentIngestionRequest
{
    /// <summary>
    /// Gets or initializes the stable document identifier used in source metadata.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets or initializes the human-readable document title shown in source metadata.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets or initializes the plain-text document body that will be chunked and embedded.
    /// </summary>
    public string? Content { get; init; }
}

