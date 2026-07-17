namespace Runiq.AI.Rag.Runtime;

/// <summary>Identifies the lifecycle state of a managed ingestion operation.</summary>
public enum RagIngestionOperationState
{
    /// <summary>The operation was accepted but has not begun provider work.</summary>
    Pending,
    /// <summary>The operation is executing.</summary>
    Running,
    /// <summary>Cancellation was requested and is propagating.</summary>
    Cancelling,
    /// <summary>The operation completed without document failures.</summary>
    Completed,
    /// <summary>The operation completed with one or more document failures.</summary>
    PartiallyCompleted,
    /// <summary>The operation encountered a fatal source or pipeline failure.</summary>
    Failed,
    /// <summary>The operation stopped because cancellation was requested.</summary>
    Cancelled
}

/// <summary>Identifies why a managed ingestion operation started.</summary>
public enum RagIngestionOperationReason
{
    /// <summary>An explicit programmatic request started the operation.</summary>
    Manual,
    /// <summary>Blocking application startup started the operation.</summary>
    Startup,
    /// <summary>Non-blocking application startup started the operation.</summary>
    BackgroundStartup,
    /// <summary>The local in-process schedule started the operation.</summary>
    Scheduled
}

/// <summary>Describes whether an index currently has usable ingested content.</summary>
public enum RagIndexReadiness
{
    /// <summary>No usable ingestion has completed.</summary>
    NotInitialized,
    /// <summary>The first ingestion is active.</summary>
    Initializing,
    /// <summary>A usable ingestion is available.</summary>
    Ready,
    /// <summary>A usable ingestion remains available after a later failure.</summary>
    Degraded,
    /// <summary>No usable ingestion exists and initialization failed.</summary>
    Failed
}

/// <summary>Provides a safe structured ingestion failure.</summary>
public sealed record RagIngestionRuntimeFailure
{
    /// <summary>Gets the provider-independent error category.</summary>
    public required string Code { get; init; }
    /// <summary>Gets the safe failure message.</summary>
    public required string Message { get; init; }
    /// <summary>Gets the safe source identity, when available.</summary>
    public string? SourceIdentity { get; init; }
    /// <summary>Gets the safe document identity, when available.</summary>
    public string? DocumentIdentity { get; init; }
    /// <summary>Gets the failure timestamp.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>Provides an immutable snapshot of managed ingestion progress.</summary>
public sealed record RagIngestionProgress
{
    /// <summary>Gets the number of discovered documents.</summary>
    public int DiscoveredDocuments { get; init; }
    /// <summary>Gets the number of processed documents.</summary>
    public int ProcessedDocuments { get; init; }
    /// <summary>Gets the number of added documents.</summary>
    public int AddedDocuments { get; init; }
    /// <summary>Gets the number of updated documents.</summary>
    public int UpdatedDocuments { get; init; }
    /// <summary>Gets the number of skipped documents.</summary>
    public int SkippedDocuments { get; init; }
    /// <summary>Gets the number of deleted documents.</summary>
    public int DeletedDocuments { get; init; }
    /// <summary>Gets the number of failed documents.</summary>
    public int FailedDocuments { get; init; }
    /// <summary>Gets the number of produced chunks.</summary>
    public int ProducedChunks { get; init; }
    /// <summary>Gets the number of produced embeddings.</summary>
    public int ProducedEmbeddings { get; init; }
    /// <summary>Gets the current safe source identity.</summary>
    public string? CurrentSource { get; init; }
    /// <summary>Gets the current safe document identity when the production pipeline can report one.</summary>
    public string? CurrentDocument { get; init; }
    /// <summary>Gets the most recent safe failure.</summary>
    public RagIngestionRuntimeFailure? LastFailure { get; init; }
}

/// <summary>Provides an immutable snapshot of one managed ingestion operation.</summary>
public sealed record RagIngestionOperation
{
    /// <summary>Gets the unique operation identifier.</summary>
    public required Guid OperationId { get; init; }
    /// <summary>Gets the registered index name.</summary>
    public required string IndexName { get; init; }
    /// <summary>Gets the operation reason.</summary>
    public required RagIngestionOperationReason Reason { get; init; }
    /// <summary>Gets the operation lifecycle state.</summary>
    public required RagIngestionOperationState State { get; init; }
    /// <summary>Gets the operation start time.</summary>
    public required DateTimeOffset StartedAt { get; init; }
    /// <summary>Gets the completion time for a terminal operation.</summary>
    public DateTimeOffset? CompletedAt { get; init; }
    /// <summary>Gets the elapsed operation duration.</summary>
    public TimeSpan Duration { get; init; }
    /// <summary>Gets the latest immutable progress snapshot.</summary>
    public required RagIngestionProgress Progress { get; init; }
}

/// <summary>Provides runtime state for a registered RAG index.</summary>
public sealed record RagIndexRuntimeStatus
{
    /// <summary>Gets the registered index name.</summary>
    public required string IndexName { get; init; }
    /// <summary>Gets the query-readiness state.</summary>
    public required RagIndexReadiness Readiness { get; init; }
    /// <summary>Gets the active operation, when present.</summary>
    public RagIngestionOperation? ActiveOperation { get; init; }
    /// <summary>Gets the most recent terminal operation, when present.</summary>
    public RagIngestionOperation? LastOperation { get; init; }
    /// <summary>Gets the time at which this runtime state last changed.</summary>
    public required DateTimeOffset LastUpdatedAt { get; init; }
}

/// <summary>Coordinates managed ingestion operations for registered RAG indexes.</summary>
public interface IRagIngestionManager
{
    /// <summary>Starts explicit ingestion and completes when the operation reaches a terminal state.</summary>
    /// <param name="indexName">The registered index name.</param>
    /// <param name="cancellationToken">Cancels the caller's operation.</param>
    /// <returns>The terminal operation snapshot.</returns>
    Task<RagIngestionOperation> StartAsync(string indexName, CancellationToken cancellationToken = default);
    /// <summary>Gets the current runtime status for a registered index.</summary>
    /// <param name="indexName">The registered index name.</param>
    /// <returns>The immutable runtime status snapshot.</returns>
    RagIndexRuntimeStatus GetStatus(string indexName);
    /// <summary>Requests cancellation of the active operation and waits for it to stop.</summary>
    /// <param name="indexName">The registered index name.</param>
    /// <param name="cancellationToken">Cancels waiting for completion.</param>
    Task CancelAsync(string indexName, CancellationToken cancellationToken = default);
}
