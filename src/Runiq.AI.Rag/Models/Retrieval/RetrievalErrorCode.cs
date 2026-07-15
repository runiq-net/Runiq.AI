namespace Runiq.AI.Rag.Models.Retrieval;

/// <summary>
/// Enumerates the provider-independent error categories that a query-time retrieval operation can report.
/// These categories let callers branch on why retrieval did not produce results without depending on any
/// embedding provider, vector store, or SDK-specific exception type. An empty result set is never modeled as
/// an error; it is a successful retrieval that simply matched nothing.
/// </summary>
public enum RetrievalErrorCode
{
    /// <summary>
    /// No error occurred. This is the value carried by every successful <see cref="RetrievalResult"/>,
    /// including successful retrievals that returned an empty item list.
    /// </summary>
    None = 0,

    /// <summary>
    /// The retrieval request did not carry provider-independent data that is meaningful for retrieval — for
    /// example a request that provides neither query text nor a query vector. This category represents an
    /// invalid request deterministically instead of surfacing it as a store or provider failure.
    /// </summary>
    InvalidRequest = 1,

    /// <summary>
    /// Retrieval was attempted with a valid request but could not complete — for example because the target
    /// index was unavailable or the underlying store rejected the query. Provider-specific exception types,
    /// messages, and SDK details are never surfaced through this error code.
    /// </summary>
    RetrievalFailed = 2,

    /// <summary>
    /// The query text could not be turned into a query vector: the embedding abstraction either failed or
    /// produced an empty embedding. When retrieval reports this category the vector store was never queried.
    /// Provider-specific exception types, messages, and SDK details are never surfaced through this error code.
    /// </summary>
    EmbeddingFailed = 3,

    /// <summary>
    /// The query vector was produced successfully but the vector store query itself failed — either the store
    /// reported an unsuccessful result or raised an error while executing the query. Provider-specific
    /// exception types, messages, and SDK details are never surfaced through this error code.
    /// </summary>
    VectorStoreQueryFailed = 4,
}

