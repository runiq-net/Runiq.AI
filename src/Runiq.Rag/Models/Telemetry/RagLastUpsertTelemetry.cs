using Runiq.Rag.Models.VectorStores;

namespace Runiq.Rag.Models.Telemetry;

/// <summary>
/// Carries a read-only snapshot of the most recent vector store upsert operation observed by the RAG
/// telemetry recorder. The snapshot describes the last operation only; it is not a live view of the
/// vector store contents.
/// </summary>
public sealed class RagLastUpsertTelemetry
{
    /// <summary>
    /// Gets a value indicating whether the last upsert operation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the provider-independent error category of the last upsert operation.
    /// <see cref="VectorStoreUpsertErrorCode.None"/> for successful operations.
    /// </summary>
    public VectorStoreUpsertErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Gets the provider-independent, human-readable failure reason of the last upsert operation.
    /// Empty for successful operations.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of chunk vector records involved in the last upsert operation: the processed
    /// count when the operation succeeded, otherwise the attempted count. This reflects the last
    /// operation only, not a total record count of the vector store.
    /// </summary>
    public int ChunkCount { get; init; }

    /// <summary>
    /// Gets the point in time at which the last upsert operation outcome was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}
