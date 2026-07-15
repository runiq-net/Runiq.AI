namespace Runiq.AI.Core.Rag;

/// <summary>
/// Describes read-only RAG configuration information for dashboard display.
/// </summary>
public sealed class RuniqRagInfo
{
    /// <summary>
    /// Gets a value indicating whether RAG services are registered in the host application.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the configured vector store provider label, or null when RAG is not registered.
    /// </summary>
    public string? VectorStore { get; init; }

    /// <summary>
    /// Gets the configured default vector index name, or null when no default index name is configured.
    /// </summary>
    public string? IndexName { get; init; }

    /// <summary>
    /// Gets the configured default number of search results to retrieve, or null when RAG is not registered.
    /// </summary>
    public int? DefaultTopK { get; init; }

    /// <summary>
    /// Gets the embedding vector dimension when a registered RAG service exposes it deterministically.
    /// The current RAG abstractions do not expose the dimension without executing an embedding operation,
    /// so this value is null until a deterministic configuration source is available.
    /// </summary>
    public int? EmbeddingDimension { get; init; }

    /// <summary>
    /// Gets the most recently recorded upsert operation, or null when no upsert telemetry is available.
    /// </summary>
    public RuniqRagLastUpsertInfo? LastUpsert { get; init; }

    /// <summary>
    /// Gets the most recently recorded retrieval operation, or null when no retrieval telemetry is available.
    /// </summary>
    public RuniqRagLastRetrievalInfo? LastRetrieval { get; init; }

    /// <summary>
    /// Gets a developer-readable description of RAG configuration problems, or null when none were detected.
    /// </summary>
    public string? Diagnostics { get; init; }
}

/// <summary>
/// Describes the last recorded RAG vector upsert operation for dashboard display.
/// </summary>
public sealed class RuniqRagLastUpsertInfo
{
    /// <summary>
    /// Gets a value indicating whether the last upsert operation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the provider-independent error category as a developer-readable label.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the provider-independent failure reason, or an empty string when the operation succeeded.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of chunks involved in the last upsert operation. This is not a live vector store total.
    /// </summary>
    public int ChunkCount { get; init; }

    /// <summary>
    /// Gets the point in time at which the last upsert outcome was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Describes the last recorded RAG retrieval operation for dashboard display.
/// </summary>
public sealed class RuniqRagLastRetrievalInfo
{
    /// <summary>
    /// Gets a value indicating whether the last retrieval operation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the provider-independent error category as a developer-readable label.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the provider-independent failure reason, or an empty string when the operation succeeded.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of results returned by the last retrieval operation.
    /// </summary>
    public int ResultCount { get; init; }

    /// <summary>
    /// Gets the elapsed time of the last retrieval operation in milliseconds.
    /// </summary>
    public double DurationMilliseconds { get; init; }

    /// <summary>
    /// Gets the point in time at which the last retrieval outcome was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

