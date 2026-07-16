using Runiq.AI.Rag.Models.Metadata;

namespace Runiq.AI.Rag.Models.Ingestion;

/// <summary>Represents application-provided source content before it is parsed and chunked.</summary>
public sealed class RagSourceDocument
{
    /// <summary>Gets or initializes a stable identifier for this document.</summary>
    public required string Id { get; init; }
    /// <summary>Gets or initializes the source text or raw file content.</summary>
    public required string Content { get; init; }
    /// <summary>Gets or initializes the content type. Supported values include text/plain, text/markdown and application/json.</summary>
    public string ContentType { get; init; } = "text/plain";
    /// <summary>Gets or initializes a human-readable title.</summary>
    public string? Title { get; init; }
    /// <summary>Gets or initializes the source path or URI.</summary>
    public string? Source { get; init; }
    /// <summary>Gets or initializes an optional producer-controlled version.</summary>
    public string? Version { get; init; }
    /// <summary>Gets or initializes filterable source metadata.</summary>
    public RagMetadata Metadata { get; init; } = RagMetadata.Empty;
}
