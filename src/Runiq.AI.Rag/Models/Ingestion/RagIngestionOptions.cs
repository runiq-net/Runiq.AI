namespace Runiq.AI.Rag.Models.Ingestion;

/// <summary>Configures the unified document ingestion pipeline.</summary>
public sealed class RagIngestionOptions
{
    /// <summary>Gets or sets the JSON property containing text. Nested paths are not supported.</summary>
    public string JsonTextField { get; set; } = "text";
    /// <summary>Gets or sets the maximum source document size in characters.</summary>
    public int MaximumDocumentCharacters { get; set; } = 2_000_000;
    /// <summary>Gets or sets the maximum number of documents processed concurrently.</summary>
    public int MaxConcurrency { get; set; } = 4;
    /// <summary>Gets or sets the maximum number of chunk texts sent in one embedding provider request.</summary>
    public int EmbeddingBatchSize { get; set; } = 64;
    /// <summary>Gets or sets the optional path for the durable ingestion manifest. Empty uses a process-local manifest.</summary>
    public string? StatePath { get; set; }
    /// <summary>Gets or sets whether missing documents are removed from the selected source and index.</summary>
    public bool PropagateDeletes { get; set; }
    /// <summary>Gets or sets whether failures stop the run immediately.</summary>
    public bool FailFast { get; set; }
}
