namespace Runiq.AI.Rag.Abstractions.Retrieval;

/// <summary>
/// Identifies a genuine retrieval execution failure after the retriever and its required infrastructure
/// have been resolved and validated. Optional Agent RAG execution may continue without grounding only for
/// this explicit failure category.
/// </summary>
public sealed class RagRetrievalExecutionException : Exception
{
    /// <summary>
    /// Creates a retrieval execution failure with a diagnostic message and optional underlying backend error.
    /// </summary>
    /// <param name="message">A description of the failed retrieval operation.</param>
    /// <param name="innerException">The backend error that caused retrieval to fail, when available.</param>
    public RagRetrievalExecutionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
