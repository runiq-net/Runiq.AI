using Runiq.Rag.Abstractions.Retrieval;
using Runiq.Rag.Abstractions.Tools;
using Runiq.Rag.Models.Retrieval;
using Runiq.Rag.Models.Tools;

namespace Runiq.Rag.Tools;

/// <summary>
/// Default <see cref="IVectorQueryTool"/> implementation that runs an agent-supplied query by adapting the
/// provider-independent <see cref="VectorQueryToolRequest"/> to the existing query-time retrieval flow. It
/// delegates execution to <see cref="IRagRetrievalPipeline"/> — forwarding the query text, index name, top-k
/// value, and metadata filter — and maps the resulting <see cref="RetrievalResult"/> back into a
/// <see cref="VectorQueryToolResult"/>. The tool owns no embedding provider or vector store: it neither
/// introduces a new retrieval pipeline nor resolves a concrete provider. <see cref="VectorQueryToolRequest.VectorStoreName"/>
/// and <see cref="VectorQueryToolRequest.EmbeddingModel"/> are accepted as association/configuration values;
/// they are not routed to a provider in this unit, so the retrieval pipeline's configured embedding model and
/// vector store are used.
/// </summary>
/// <remarks>
/// Error handling boundary: only an already-cancelled <see cref="CancellationToken"/> is surfaced as an
/// exception, matching the cancellation standard of the retrieval pipeline it delegates to. Every other failure
/// is returned as a failed <see cref="VectorQueryToolResult"/> with a deterministic
/// <see cref="RetrievalErrorCode"/>. A null request or a missing vector store name — which the retrieval
/// pipeline cannot see — is reported here as <see cref="RetrievalErrorCode.InvalidRequest"/> before the pipeline
/// is invoked; the remaining semantic checks (missing index name, empty query, non-positive top-k, embedding
/// and vector store failures) are reported by the retrieval pipeline and mapped straight through.
/// </remarks>
public sealed class DefaultVectorQueryTool : IVectorQueryTool
{
    private const string NullRequestReason = "Vector query tool request is required.";
    private const string MissingVectorStoreNameReason = "Vector query tool request vector store name must be a non-empty, non-whitespace value.";

    private readonly IRagRetrievalPipeline retrievalPipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultVectorQueryTool"/> class.
    /// </summary>
    /// <param name="retrievalPipeline">The existing provider-independent retrieval pipeline the tool delegates to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="retrievalPipeline"/> is null.</exception>
    public DefaultVectorQueryTool(IRagRetrievalPipeline retrievalPipeline)
    {
        this.retrievalPipeline = retrievalPipeline ?? throw new ArgumentNullException(nameof(retrievalPipeline));
    }

    /// <inheritdoc />
    public async Task<VectorQueryToolResult> ExecuteAsync(
        VectorQueryToolRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationFailure = ValidateRequest(request);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        var retrievalRequest = new RetrievalRequest
        {
            IndexName = request.IndexName,
            QueryText = request.QueryText,
            TopK = request.TopK,
            MetadataFilter = request.MetadataFilter,
        };

        var retrievalResult = await retrievalPipeline
            .RetrieveAsync(retrievalRequest, cancellationToken)
            .ConfigureAwait(false);

        return MapResult(retrievalResult);
    }

    /// <summary>
    /// Validates the parts of the request the retrieval pipeline cannot see before it is invoked. Returns a
    /// failed result with <see cref="RetrievalErrorCode.InvalidRequest"/> when the request is null or carries no
    /// vector store name; returns null when the request is valid enough to delegate, leaving the remaining
    /// semantic validation (index name, query, top-k) to the retrieval pipeline.
    /// </summary>
    private static VectorQueryToolResult? ValidateRequest(VectorQueryToolRequest? request)
    {
        if (request is null)
        {
            return VectorQueryToolResult.Failure(RetrievalErrorCode.InvalidRequest, NullRequestReason);
        }

        if (string.IsNullOrWhiteSpace(request.VectorStoreName))
        {
            return VectorQueryToolResult.Failure(RetrievalErrorCode.InvalidRequest, MissingVectorStoreNameReason);
        }

        return null;
    }

    /// <summary>
    /// Maps the retrieval pipeline outcome into the tool result, reusing the retrieval matches, error category,
    /// reason, and metadata unchanged rather than duplicating retrieval machinery.
    /// </summary>
    private static VectorQueryToolResult MapResult(RetrievalResult retrievalResult)
    {
        return retrievalResult.Succeeded
            ? VectorQueryToolResult.Success(retrievalResult.Items, retrievalResult.Metadata)
            : VectorQueryToolResult.Failure(
                retrievalResult.ErrorCode,
                retrievalResult.Reason,
                retrievalResult.Metadata);
    }
}
