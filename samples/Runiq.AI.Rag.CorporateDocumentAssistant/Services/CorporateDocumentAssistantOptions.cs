namespace Runiq.AI.Rag.CorporateDocumentAssistant.Services;

/// <summary>
/// Configures sample-only Corporate Document Assistant settings.
/// </summary>
public sealed class CorporateDocumentAssistantOptions
{
    /// <summary>
    /// Gets or sets the vector index name used by the sample ingestion flow.
    /// </summary>
    public string IndexName { get; set; } = "corporate-document-assistant";
}

