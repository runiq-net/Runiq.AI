namespace Runiq.AI.Rag.Abstractions.Retrieval;

using Runiq.AI.Rag.Models.Retrieval;

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
        : this(message, RetrievalErrorCode.RetrievalFailed, innerException)
    {
    }

    /// <summary>
    /// Creates a retrieval execution failure with a provider-independent classification.
    /// </summary>
    /// <param name="message">A description of the failed retrieval operation.</param>
    /// <param name="errorCode">The provider-independent category of the retrieval failure.</param>
    /// <param name="innerException">The backend error that caused retrieval to fail, when available.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="errorCode"/> is undefined or represents a successful retrieval.
    /// </exception>
    public RagRetrievalExecutionException(
        string message,
        RetrievalErrorCode errorCode,
        Exception? innerException = null)
        : base(message, innerException)
    {
        if (!Enum.IsDefined(errorCode) || errorCode == RetrievalErrorCode.None)
        {
            throw new ArgumentException("A retrieval execution failure requires a defined failure error code.", nameof(errorCode));
        }

        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the provider-independent category of the retrieval failure.
    /// </summary>
    public RetrievalErrorCode ErrorCode { get; }
}
