using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.Abstractions.Telemetry;

/// <summary>
/// Records the outcome of RAG upsert and retrieval operations for read-only visibility purposes.
/// Recording is strictly observational: implementations must not mutate the supplied results, must not
/// influence pipeline behavior, and should never throw. The pipelines additionally guard every record
/// call so that a recording failure can never alter an operation result.
/// </summary>
public interface IRagOperationTelemetryRecorder
{
    /// <summary>
    /// Records the outcome of a completed vector store upsert operation.
    /// </summary>
    /// <param name="result">The upsert pipeline result to record. A null value is ignored.</param>
    void RecordUpsert(UpsertVectorResult result);

    /// <summary>
    /// Records the outcome of a completed query-time retrieval operation.
    /// </summary>
    /// <param name="result">The retrieval pipeline result to record. A null value is ignored.</param>
    /// <param name="duration">The elapsed time of the retrieval operation as measured by the caller.</param>
    void RecordRetrieval(RetrievalResult result, TimeSpan duration);
}

