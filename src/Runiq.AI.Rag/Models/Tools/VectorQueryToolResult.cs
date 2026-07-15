using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.Models.Tools;

/// <summary>
/// Carries the provider-independent outcome of a Vector Query Tool invocation in an agent-usable shape. The
/// result reuses the existing retrieval contract rather than introducing a parallel one: matches are exposed as
/// <see cref="RetrievalResultItem"/> values (preserving the content, similarity score, and metadata produced by
/// the retrieval pipeline) and failures reuse the existing <see cref="RetrievalErrorCode"/> categories. It
/// draws the same line between three states as <see cref="RetrievalResult"/>: a successful invocation that
/// returned matches, a successful invocation that matched nothing (an empty <see cref="Matches"/> list, which
/// is never treated as a failure), and a failed invocation that reports a deterministic
/// <see cref="RetrievalErrorCode"/> without leaking provider-specific error details.
/// </summary>
public sealed class VectorQueryToolResult
{
    private IReadOnlyList<RetrievalResultItem> matches = Array.Empty<RetrievalResultItem>();
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorQueryToolResult"/> class.
    /// </summary>
    private VectorQueryToolResult()
    {
    }

    /// <summary>
    /// Creates a successful tool result. An empty or null match list represents a successful invocation that
    /// matched nothing, not a failure.
    /// </summary>
    /// <param name="matches">The retrieved matches ordered best match first, or null for an empty success.</param>
    /// <param name="metadata">Provider-independent metadata that describes the invocation outcome.</param>
    /// <returns>A successful tool result with <see cref="RetrievalErrorCode.None"/>.</returns>
    public static VectorQueryToolResult Success(
        IReadOnlyList<RetrievalResultItem>? matches = null,
        RagMetadata? metadata = null)
    {
        return new VectorQueryToolResult
        {
            Succeeded = true,
            ErrorCode = RetrievalErrorCode.None,
            Reason = string.Empty,
            Matches = matches ?? Array.Empty<RetrievalResultItem>(),
            Metadata = metadata ?? RagMetadata.Empty,
        };
    }

    /// <summary>
    /// Creates a failed tool result with a deterministic provider-independent error category reused from the
    /// retrieval contract.
    /// </summary>
    /// <param name="errorCode">The non-<see cref="RetrievalErrorCode.None"/> category that describes the failure.</param>
    /// <param name="reason">A provider-independent, human-readable failure reason.</param>
    /// <param name="metadata">Provider-independent metadata that describes the invocation failure.</param>
    /// <returns>A failed tool result with an empty match list.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errorCode"/> is <see cref="RetrievalErrorCode.None"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reason"/> is null.</exception>
    public static VectorQueryToolResult Failure(
        RetrievalErrorCode errorCode,
        string reason,
        RagMetadata? metadata = null)
    {
        if (errorCode == RetrievalErrorCode.None)
        {
            throw new ArgumentException("A failed tool result requires a non-None error code.", nameof(errorCode));
        }

        ArgumentNullException.ThrowIfNull(reason);

        return new VectorQueryToolResult
        {
            Succeeded = false,
            ErrorCode = errorCode,
            Reason = reason,
            Matches = Array.Empty<RetrievalResultItem>(),
            Metadata = metadata ?? RagMetadata.Empty,
        };
    }

    /// <summary>
    /// Gets a value indicating whether the tool invocation completed successfully. A successful invocation may
    /// still carry an empty <see cref="Matches"/> list when nothing matched the query.
    /// </summary>
    public bool Succeeded { get; private init; }

    /// <summary>
    /// Gets the provider-independent error category for this result, reused from the retrieval contract.
    /// Defaults to <see cref="RetrievalErrorCode.None"/>, which every successful result — including
    /// empty-but-successful invocations — carries. Failed results carry a non-<see cref="RetrievalErrorCode.None"/>
    /// value so agents can branch on the failure category without inspecting <see cref="Reason"/>.
    /// </summary>
    public RetrievalErrorCode ErrorCode { get; private init; }

    /// <summary>
    /// Gets a provider-independent, human-readable failure reason when the invocation did not succeed. It is
    /// empty for successful results and never contains provider-specific exception text.
    /// </summary>
    public string Reason { get; private init; } = string.Empty;

    /// <summary>
    /// Gets the retrieved matches ordered best match first, exposed in the existing
    /// <see cref="RetrievalResultItem"/> shape so agents receive content, similarity score, and metadata without
    /// binding to a vector store provider. A null value is normalized to an empty collection, and an empty
    /// collection represents a successful invocation that matched nothing.
    /// </summary>
    public IReadOnlyList<RetrievalResultItem> Matches
    {
        get => matches;
        private init => matches = value ?? Array.Empty<RetrievalResultItem>();
    }

    /// <summary>
    /// Gets provider-independent result metadata that describes the invocation outcome without exposing
    /// provider-specific details. A null value is rejected so agents can always rely on a non-null metadata
    /// instance.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    public RagMetadata Metadata
    {
        get => metadata;
        private init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}

