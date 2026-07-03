using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.Retrieval;

/// <summary>
/// Carries the provider-independent outcome of a query-time retrieval. The result draws a clear line between
/// three states: a successful retrieval that returned matches, a successful retrieval that matched nothing
/// (an empty <see cref="Items"/> list, which is never treated as a failure), and a failed retrieval that
/// reports a deterministic <see cref="RetrievalErrorCode"/> without leaking provider-specific error details.
/// </summary>
public sealed class RetrievalResult
{
    private IReadOnlyList<RetrievalResultItem> items = Array.Empty<RetrievalResultItem>();
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalResult"/> class.
    /// </summary>
    private RetrievalResult()
    {
    }

    /// <summary>
    /// Creates a successful retrieval result. An empty or null item list represents a successful retrieval
    /// that matched nothing, not a failure.
    /// </summary>
    /// <param name="items">The retrieved matches ordered best match first, or null for an empty success.</param>
    /// <param name="metadata">Provider-independent metadata that describes the retrieval outcome.</param>
    /// <returns>A successful retrieval result with <see cref="RetrievalErrorCode.None"/>.</returns>
    public static RetrievalResult Success(
        IReadOnlyList<RetrievalResultItem>? items = null,
        RagMetadata? metadata = null)
    {
        return new RetrievalResult
        {
            Succeeded = true,
            ErrorCode = RetrievalErrorCode.None,
            Reason = string.Empty,
            Items = items ?? Array.Empty<RetrievalResultItem>(),
            Metadata = metadata ?? RagMetadata.Empty,
        };
    }

    /// <summary>
    /// Creates a failed retrieval result with a deterministic provider-independent error category.
    /// </summary>
    /// <param name="errorCode">The non-<see cref="RetrievalErrorCode.None"/> category that describes the failure.</param>
    /// <param name="reason">A provider-independent, human-readable failure reason.</param>
    /// <param name="metadata">Provider-independent metadata that describes the retrieval failure.</param>
    /// <returns>A failed retrieval result with an empty item list.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errorCode"/> is <see cref="RetrievalErrorCode.None"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reason"/> is null.</exception>
    public static RetrievalResult Failure(
        RetrievalErrorCode errorCode,
        string reason,
        RagMetadata? metadata = null)
    {
        if (errorCode == RetrievalErrorCode.None)
        {
            throw new ArgumentException("A failed retrieval result requires a non-None error code.", nameof(errorCode));
        }

        ArgumentNullException.ThrowIfNull(reason);

        return new RetrievalResult
        {
            Succeeded = false,
            ErrorCode = errorCode,
            Reason = reason,
            Items = Array.Empty<RetrievalResultItem>(),
            Metadata = metadata ?? RagMetadata.Empty,
        };
    }

    /// <summary>
    /// Gets or initializes a value indicating whether the retrieval completed successfully. A successful
    /// retrieval may still carry an empty <see cref="Items"/> list when nothing matched the query.
    /// </summary>
    public bool Succeeded { get; private init; }

    /// <summary>
    /// Gets or initializes the provider-independent error category for this result. Defaults to
    /// <see cref="RetrievalErrorCode.None"/>, which every successful result — including empty-but-successful
    /// retrievals — carries. Failed results carry a non-<see cref="RetrievalErrorCode.None"/> value so callers
    /// can branch on the failure category without inspecting <see cref="Reason"/>.
    /// </summary>
    public RetrievalErrorCode ErrorCode { get; private init; }

    /// <summary>
    /// Gets or initializes a provider-independent, human-readable failure reason when the retrieval did not
    /// succeed. It is empty for successful results and never contains provider-specific exception text.
    /// </summary>
    public string Reason { get; private init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the retrieved matches ordered best match first. A null value is normalized to an
    /// empty collection so consumers can iterate the results without null checks, and an empty collection
    /// represents a successful retrieval that matched nothing.
    /// </summary>
    public IReadOnlyList<RetrievalResultItem> Items
    {
        get => items;
        private init => items = value ?? Array.Empty<RetrievalResultItem>();
    }

    /// <summary>
    /// Gets or initializes provider-independent result metadata that describes the retrieval outcome without
    /// exposing provider-specific details. A null value is rejected so consumers can always rely on a non-null
    /// metadata instance.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    public RagMetadata Metadata
    {
        get => metadata;
        private init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
