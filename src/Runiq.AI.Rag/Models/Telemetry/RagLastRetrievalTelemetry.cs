using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.Models.Telemetry;

/// <summary>
/// Carries a read-only snapshot of the most recent query-time retrieval operation observed by the RAG
/// telemetry recorder. The snapshot describes the last operation only.
/// </summary>
public sealed class RagLastRetrievalTelemetry
{
    /// <summary>
    /// Gets a value indicating whether the last retrieval operation completed successfully. A successful
    /// retrieval may still carry a <see cref="ResultCount"/> of zero when nothing matched the query.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the provider-independent error category of the last retrieval operation.
    /// <see cref="RetrievalErrorCode.None"/> for successful operations.
    /// </summary>
    public RetrievalErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Gets the provider-independent, human-readable failure reason of the last retrieval operation.
    /// Empty for successful operations.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of matches returned by the last retrieval operation. Zero for failed operations
    /// and for successful operations that matched nothing.
    /// </summary>
    public int ResultCount { get; init; }

    /// <summary>
    /// Gets the elapsed time of the last retrieval operation as measured around the retrieval pipeline.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the point in time at which the last retrieval operation outcome was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

