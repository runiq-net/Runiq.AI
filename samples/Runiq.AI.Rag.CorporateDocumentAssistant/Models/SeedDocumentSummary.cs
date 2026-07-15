namespace Runiq.AI.Rag.CorporateDocumentAssistant.Models;

/// <summary>
/// Summarizes a checked-in seed document available to the sample application.
/// </summary>
public sealed class SeedDocumentSummary
{
    /// <summary>
    /// Gets the seed document identifier derived from the file name.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the seed document file name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a short preview of the seed document content.
    /// </summary>
    public required string Preview { get; init; }

    /// <summary>
    /// Gets the endpoint URL that returns the full seed document text.
    /// </summary>
    public required string Url { get; init; }
}

