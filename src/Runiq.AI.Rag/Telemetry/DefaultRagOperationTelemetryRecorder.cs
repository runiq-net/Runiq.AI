using Runiq.AI.Rag.Abstractions.Telemetry;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Telemetry;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.Telemetry;

/// <summary>
/// Default in-memory RAG operation telemetry recorder. It keeps only the most recent upsert and
/// retrieval snapshots, never persists anything, never throws from a record call, and reads the
/// recording timestamp from an injectable <see cref="TimeProvider"/> so tests stay deterministic.
/// Snapshot fields are copied from the existing pipeline result contracts without modifying them.
/// </summary>
public sealed class DefaultRagOperationTelemetryRecorder :
    IRagOperationTelemetryRecorder,
    IRagOperationTelemetryReader
{
    private readonly TimeProvider timeProvider;

    private volatile RagLastUpsertTelemetry? lastUpsert;
    private volatile RagLastRetrievalTelemetry? lastRetrieval;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRagOperationTelemetryRecorder"/> class.
    /// </summary>
    /// <param name="timeProvider">
    /// The time source used for recording timestamps. Null falls back to <see cref="TimeProvider.System"/>.
    /// </param>
    public DefaultRagOperationTelemetryRecorder(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public RagLastUpsertTelemetry? LastUpsert => lastUpsert;

    /// <inheritdoc />
    public RagLastRetrievalTelemetry? LastRetrieval => lastRetrieval;

    /// <inheritdoc />
    public void RecordUpsert(UpsertVectorResult result)
    {
        if (result is null)
        {
            return;
        }

        lastUpsert = new RagLastUpsertTelemetry
        {
            Succeeded = result.Succeeded,
            ErrorCode = result.ErrorCode,
            Reason = result.Reason,
            ChunkCount = result.Succeeded ? result.ProcessedCount : result.AttemptedCount,
            Timestamp = timeProvider.GetUtcNow(),
        };
    }

    /// <inheritdoc />
    public void RecordRetrieval(RetrievalResult result, TimeSpan duration)
    {
        if (result is null)
        {
            return;
        }

        lastRetrieval = new RagLastRetrievalTelemetry
        {
            Succeeded = result.Succeeded,
            ErrorCode = result.ErrorCode,
            Reason = result.Reason,
            ResultCount = result.Items.Count,
            Duration = duration,
            Timestamp = timeProvider.GetUtcNow(),
        };
    }
}

