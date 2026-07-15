using Microsoft.Extensions.Options;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.Models;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Embeddings;
using Runiq.AI.Rag.Abstractions.Telemetry;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.Retrieval;

/// <summary>
/// Runs the default query-time retrieval pipeline by combining <see cref="IEmbeddingClient"/> and
/// <see cref="IRagVectorStore"/>. This is an orchestration layer only: it embeds the query text through the
/// embedding abstraction, forwards the resulting query vector together with the request's index name, top-k
/// value, and metadata filter to the vector store query operation, and maps the store's matches into the
/// standard <see cref="RetrievalResult"/>. It contains no provider-specific logic and computes no similarity
/// scores of its own — scores are taken from the vector store query result.
/// </summary>
/// <remarks>
/// Error handling boundary: only an already-cancelled <see cref="CancellationToken"/> is surfaced as an
/// exception, matching the cancellation standard of the other RAG pipelines. Every other failure — a null
/// request, a missing index name, a non-positive top-k value, a request without retrievable query input, an
/// embedding abstraction that throws or produces an empty embedding, and a vector store query that throws or
/// reports an unsuccessful result — is normalized into a failed <see cref="RetrievalResult"/> with a
/// deterministic <see cref="RetrievalErrorCode"/> so that no provider-specific exception type, message, or
/// SDK detail crosses this pipeline's public contract. When request validation or embedding fails, the vector
/// store is never queried, and when request validation fails the embedding abstraction is never invoked
/// either.
/// </remarks>
public sealed class DefaultRagRetrievalPipeline : IRagRetrievalPipeline
{
    private const string NullRequestReason = "Retrieval request is required.";
    private const string MissingIndexNameReason = "Retrieval request index name must be a non-empty, non-whitespace value.";
    private const string InvalidTopKReason = "Retrieval request TopK must be greater than zero.";
    private const string MissingQueryReason = "Retrieval request must carry non-empty query text or a non-empty query vector.";
    private const string EmbeddingFailedReason = "Query embedding generation failed.";
    private const string VectorStoreQueryFailedReason = "Vector store query failed.";

    private readonly IEmbeddingClient embeddingClient;
    private readonly IRagVectorStore vectorStore;
    private readonly IRagOperationTelemetryRecorder? telemetryRecorder;
    private readonly TimeProvider timeProvider;
    private readonly RagOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRagRetrievalPipeline"/> class.
    /// </summary>
    /// <param name="embeddingClient">The Core embedding client used to embed query text.</param>
    /// <param name="vectorStore">The provider-independent vector store contract used to run the similarity query.</param>
    /// <param name="telemetryRecorder">
    /// The optional, strictly observational telemetry recorder that receives each retrieval result and its
    /// measured duration. Null disables recording without changing retrieval behavior.
    /// </param>
    /// <param name="timeProvider">
    /// The time source used only to measure retrieval duration for telemetry. Null falls back to
    /// <see cref="TimeProvider.System"/>.
    /// </param>
    /// <param name="options">The RAG options used to resolve the embedding model.</param>
    public DefaultRagRetrievalPipeline(
        IEmbeddingClient embeddingClient,
        IRagVectorStore vectorStore,
        IRagOperationTelemetryRecorder? telemetryRecorder = null,
        TimeProvider? timeProvider = null,
        IOptions<RagOptions>? options = null)
    {
        this.embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        this.vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        this.telemetryRecorder = telemetryRecorder;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.options = options?.Value ?? new RagOptions();
    }

    /// <inheritdoc />
    public async Task<RetrievalResult> RetrieveAsync(
        RetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startTimestamp = timeProvider.GetTimestamp();
        var result = await RetrieveCoreAsync(request, cancellationToken).ConfigureAwait(false);

        RecordRetrieval(result, timeProvider.GetElapsedTime(startTimestamp));

        return result;
    }

    /// <summary>
    /// Runs the retrieval pipeline exactly as documented on the class contract. Kept separate from
    /// <see cref="RetrieveAsync"/> so every result boundary flows through a single telemetry recording
    /// point without changing any result or error handling behavior. Cancellation surfaced as an
    /// exception bypasses recording because no result was produced.
    /// </summary>
    private async Task<RetrievalResult> RetrieveCoreAsync(
        RetrievalRequest request,
        CancellationToken cancellationToken)
    {
        var validationFailure = ValidateRequest(request);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        var queryVector = request.QueryVector is { Count: > 0 }
            ? request.QueryVector
            : await ResolveQueryVectorAsync(request.QueryText!, cancellationToken).ConfigureAwait(false);

        if (queryVector is null || queryVector.Count == 0)
        {
            return RetrievalResult.Failure(RetrievalErrorCode.EmbeddingFailed, EmbeddingFailedReason);
        }

        QueryVectorResult queryResult;
        try
        {
            queryResult = await vectorStore.QueryAsync(
                new QueryVectorRequest
                {
                    IndexName = request.IndexName,
                    Values = queryVector,
                    TopK = request.TopK,
                    MetadataFilter = request.MetadataFilter,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return RetrievalResult.Failure(RetrievalErrorCode.VectorStoreQueryFailed, VectorStoreQueryFailedReason);
        }

        if (queryResult is null || !queryResult.Succeeded)
        {
            return RetrievalResult.Failure(RetrievalErrorCode.VectorStoreQueryFailed, VectorStoreQueryFailedReason);
        }

        return RetrievalResult.Success(MapItems(queryResult), queryResult.Metadata);
    }

    /// <summary>
    /// Forwards the retrieval result and measured duration to the optional telemetry recorder. Recording is
    /// strictly observational: when no recorder is configured nothing happens, and a recorder that throws is
    /// ignored so that telemetry can never alter the retrieval result or error handling contract.
    /// </summary>
    private void RecordRetrieval(RetrievalResult result, TimeSpan duration)
    {
        if (telemetryRecorder is null)
        {
            return;
        }

        try
        {
            telemetryRecorder.RecordRetrieval(result, duration);
        }
        catch
        {
            // Telemetry must never change retrieval behavior; recorder failures are intentionally ignored.
        }
    }

    /// <summary>
    /// Validates the retrieval request deterministically before any provider abstraction is invoked. Returns a
    /// failed result with <see cref="RetrievalErrorCode.InvalidRequest"/> when the request is null, targets no
    /// index, carries a non-positive top-k value, or carries no retrievable query input; returns null when the
    /// request is valid.
    /// </summary>
    private static RetrievalResult? ValidateRequest(RetrievalRequest? request)
    {
        if (request is null)
        {
            return RetrievalResult.Failure(RetrievalErrorCode.InvalidRequest, NullRequestReason);
        }

        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return RetrievalResult.Failure(RetrievalErrorCode.InvalidRequest, MissingIndexNameReason);
        }

        if (request.TopK <= 0)
        {
            return RetrievalResult.Failure(RetrievalErrorCode.InvalidRequest, InvalidTopKReason);
        }

        if (!request.HasRetrievableQuery)
        {
            return RetrievalResult.Failure(RetrievalErrorCode.InvalidRequest, MissingQueryReason);
        }

        return null;
    }

    /// <summary>
    /// Embeds the query text through the embedding abstraction, normalizing every non-cancellation failure to
    /// a null vector so the caller can report <see cref="RetrievalErrorCode.EmbeddingFailed"/> without leaking
    /// provider-specific exception details.
    /// </summary>
    private async Task<IReadOnlyList<float>?> ResolveQueryVectorAsync(
        string queryText,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = ResolveEmbeddingModel();
            var response = await embeddingClient.EmbedAsync(new EmbeddingRequest(model, [queryText], Dimensions: model.EmbeddingDimensions), cancellationToken).ConfigureAwait(false);
            return response.Results.SingleOrDefault()?.Vector;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private ModelReference ResolveEmbeddingModel()
    {
        if (string.IsNullOrWhiteSpace(options.EmbeddingModel)) return ModelReference.Parse("openai/rag-embedding");

        return ProviderModelReferenceResolver.Resolve(ModelReference.Parse(options.EmbeddingModel), options.EmbeddingProvider);
    }

    /// <summary>
    /// Maps the vector store's matches into standard retrieval result items, carrying over the record id,
    /// stored chunk content, metadata, and the similarity score reported by the store.
    /// </summary>
    private static IReadOnlyList<RetrievalResultItem> MapItems(QueryVectorResult queryResult)
    {
        return queryResult.Records
            .Select(record => new RetrievalResultItem
            {
                RecordId = record.Id,
                Content = record.Content,
                Metadata = record.Metadata,
                Score = record.Score,
            })
            .ToList();
    }
}

