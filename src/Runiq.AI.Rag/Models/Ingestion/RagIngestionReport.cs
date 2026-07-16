namespace Runiq.AI.Rag.Models.Ingestion;

/// <summary>Reports the observable outcome of an ingestion run.</summary>
public sealed class RagIngestionReport
{
    /// <summary>Gets or initializes the number of discovered documents.</summary>
    public int DiscoveredDocuments { get; init; }
    /// <summary>Gets or initializes the number of created documents.</summary>
    public int CreatedDocuments { get; init; }
    /// <summary>Gets or initializes the number of updated documents.</summary>
    public int UpdatedDocuments { get; init; }
    /// <summary>Gets or initializes the number of unchanged documents skipped without embedding.</summary>
    public int SkippedDocuments { get; init; }
    /// <summary>Gets or initializes the number of deleted documents propagated to the vector store.</summary>
    public int DeletedDocuments { get; init; }
    /// <summary>Gets or initializes the number of failed documents.</summary>
    public int FailedDocuments { get; init; }
    /// <summary>Gets or initializes the number of persisted chunks.</summary>
    public int CreatedChunks { get; init; }
    /// <summary>Gets or initializes the elapsed duration.</summary>
    public TimeSpan Duration { get; init; }
    /// <summary>Gets or initializes document-level failures without source content.</summary>
    public IReadOnlyList<RagIngestionFailure> Failures { get; init; } = Array.Empty<RagIngestionFailure>();
}

/// <summary>Describes a document-level ingestion failure.</summary>
public sealed class RagIngestionFailure
{
    /// <summary>Gets or initializes the document identifier.</summary>
    public required string DocumentId { get; init; }
    /// <summary>Gets or initializes a safe failure message.</summary>
    public required string Message { get; init; }
}
