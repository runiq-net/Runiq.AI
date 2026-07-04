using Runiq.Rag.Models.Telemetry;

namespace Runiq.Rag.Abstractions.Telemetry;

/// <summary>
/// Provides read-only access to the most recently recorded RAG operation telemetry. This is the query
/// surface consumed by visibility features such as the dashboard; it never triggers a RAG operation.
/// </summary>
public interface IRagOperationTelemetryReader
{
    /// <summary>
    /// Gets the snapshot of the most recent upsert operation, or null when no upsert has been recorded yet.
    /// </summary>
    RagLastUpsertTelemetry? LastUpsert { get; }

    /// <summary>
    /// Gets the snapshot of the most recent retrieval operation, or null when no retrieval has been recorded yet.
    /// </summary>
    RagLastRetrievalTelemetry? LastRetrieval { get; }
}
