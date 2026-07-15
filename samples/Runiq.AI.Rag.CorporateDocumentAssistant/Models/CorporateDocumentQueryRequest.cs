namespace Runiq.AI.Rag.CorporateDocumentAssistant.Models;

/// <summary>
/// Represents a user question submitted to the corporate document assistant sample.
/// </summary>
public sealed class CorporateDocumentQueryRequest
{
    /// <summary>
    /// Gets or initializes the natural-language question to answer from retrieved document chunks.
    /// </summary>
    public string? Question { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of source chunks to retrieve.
    /// </summary>
    public int TopK { get; init; } = 4;
}

