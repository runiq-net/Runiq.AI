namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Enumerates the provider-independent error categories that a Vector Store upsert pipeline operation can
/// report. These categories let callers branch on the stage that failed without depending on any vector store
/// provider-specific exception type or SDK detail.
/// </summary>
public enum VectorStoreUpsertErrorCode
{
    /// <summary>
    /// No error occurred. This is the value carried by every successful <see cref="UpsertVectorResult"/>.
    /// </summary>
    None = 0,

    /// <summary>
    /// The upsert request failed provider-independent vector record validation (for example, a dimension
    /// mismatch) before any write was attempted against the vector store.
    /// </summary>
    ValidationFailed = 1,

    /// <summary>
    /// The RAG ingestion output could not be mapped into a valid upsert request.
    /// </summary>
    MappingFailed = 2,

    /// <summary>
    /// The vector store rejected the upsert request or raised an error while writing records. Provider-specific
    /// exception types, messages, and SDK details are never surfaced through this error code.
    /// </summary>
    StoreFailed = 3,
}
