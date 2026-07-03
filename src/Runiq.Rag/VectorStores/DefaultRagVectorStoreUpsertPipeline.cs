using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Ingestion;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.VectorStores;

namespace Runiq.Rag.VectorStores;

/// <summary>
/// Runs the default Vector Store upsert pipeline by combining <see cref="IRagUpsertVectorRequestMapper"/>,
/// <see cref="IRagVectorRecordDimensionValidator"/>, and <see cref="IRagVectorStore"/>. This is the main
/// orchestration layer that writes RAG ingestion output to a vector store: it performs no chunking, embedding
/// generation, or provider-specific vector store work of its own.
/// </summary>
/// <remarks>
/// Error handling boundary: a null argument or an already-cancelled <see cref="CancellationToken"/> is a
/// programming error and is surfaced as an exception. Every other failure — dimension validation failures,
/// exceptions raised while mapping ingestion output, exceptions raised by the configured
/// <see cref="IRagVectorStore"/>, and a vector store result that reports <see cref="UpsertVectorResult.Succeeded"/>
/// as <see langword="false"/> without throwing — is normalized into a standard failed <see cref="UpsertVectorResult"/>
/// so that no provider-specific exception type, message, SDK detail, or raw provider result crosses this
/// pipeline's public contract. A successful vector store result is normalized the same way, so the pipeline's
/// count and partial-success fields never depend on what a specific <see cref="IRagVectorStore"/> implementation
/// chooses to report. The pipeline does not support partial success: a batch either fully succeeds or is
/// reported as a full failure, with <see cref="UpsertVectorResult.AttemptedCount"/> and
/// <see cref="UpsertVectorResult.FailedCount"/> set accordingly.
/// </remarks>
public sealed class DefaultRagVectorStoreUpsertPipeline : IRagVectorStoreUpsertPipeline
{
    private const string MappingFailedReason = "Vector record mapping failed for the supplied ingestion output.";
    private const string StoreFailedReason = "Vector store upsert failed.";

    private readonly IRagUpsertVectorRequestMapper upsertVectorRequestMapper;
    private readonly IRagVectorRecordDimensionValidator dimensionValidator;
    private readonly IRagVectorStore vectorStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRagVectorStoreUpsertPipeline"/> class.
    /// </summary>
    /// <param name="upsertVectorRequestMapper">The mapper used to convert ingestion output into an upsert request.</param>
    /// <param name="dimensionValidator">The provider-independent vector record dimension validator.</param>
    /// <param name="vectorStore">The provider-independent vector store contract used to write the upsert request.</param>
    public DefaultRagVectorStoreUpsertPipeline(
        IRagUpsertVectorRequestMapper upsertVectorRequestMapper,
        IRagVectorRecordDimensionValidator dimensionValidator,
        IRagVectorStore vectorStore)
    {
        this.upsertVectorRequestMapper = upsertVectorRequestMapper ?? throw new ArgumentNullException(nameof(upsertVectorRequestMapper));
        this.dimensionValidator = dimensionValidator ?? throw new ArgumentNullException(nameof(dimensionValidator));
        this.vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
    }

    /// <inheritdoc />
    public Task<UpsertVectorResult> UpsertAsync(
        RagDocumentIngestionResult ingestionResult,
        string indexName,
        RagDocumentMetadata? documentMetadata = null,
        int? expectedDimensions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ingestionResult);
        ArgumentNullException.ThrowIfNull(indexName);
        cancellationToken.ThrowIfCancellationRequested();

        UpsertVectorRequest mappedRequest;
        try
        {
            mappedRequest = upsertVectorRequestMapper.Map(ingestionResult, indexName, documentMetadata);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Task.FromResult(CreateMappingFailureResult(indexName, ingestionResult.Items.Count));
        }

        var request = expectedDimensions.HasValue
            ? new UpsertVectorRequest
            {
                IndexName = mappedRequest.IndexName,
                Records = mappedRequest.Records,
                ExpectedDimensions = expectedDimensions,
                Metadata = mappedRequest.Metadata,
            }
            : mappedRequest;

        return UpsertAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UpsertVectorResult> UpsertAsync(
        UpsertVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ExpectedDimensions.HasValue)
        {
            var validationResult = await dimensionValidator.ValidateAsync(
                request,
                request.ExpectedDimensions.Value,
                cancellationToken).ConfigureAwait(false);

            if (!validationResult.Succeeded)
            {
                return CreateFailedUpsertResult(validationResult, request.Records.Count);
            }
        }

        UpsertVectorResult storeResult;
        try
        {
            storeResult = await vectorStore.UpsertAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CreateStoreFailureResult(request);
        }

        return NormalizeStoreResult(storeResult, request);
    }

    /// <summary>
    /// Converts a vector store's raw <see cref="UpsertVectorResult"/> into the pipeline's standard contract.
    /// A failed store result (whether or not it looks partially successful) always becomes a full failure, and
    /// a successful store result always reports every requested record as processed, because this pipeline does
    /// not support partial success regardless of what an individual <see cref="IRagVectorStore"/> implementation
    /// reports.
    /// </summary>
    private static UpsertVectorResult NormalizeStoreResult(UpsertVectorResult storeResult, UpsertVectorRequest request)
    {
        if (storeResult is null || !storeResult.Succeeded)
        {
            return CreateStoreFailureResult(request);
        }

        return new UpsertVectorResult
        {
            Succeeded = true,
            ErrorCode = VectorStoreUpsertErrorCode.None,
            IndexName = request.IndexName,
            VectorIds = storeResult.VectorIds,
            Metadata = storeResult.Metadata,
            ProcessedCount = request.Records.Count,
            AttemptedCount = request.Records.Count,
            FailedCount = 0,
            SupportsPartialSuccess = false,
        };
    }

    private static UpsertVectorResult CreateFailedUpsertResult(
        VectorRecordDimensionValidationResult validationResult,
        int attemptedCount)
    {
        return new UpsertVectorResult
        {
            Succeeded = false,
            ErrorCode = VectorStoreUpsertErrorCode.ValidationFailed,
            Reason = validationResult.Reason,
            IndexName = validationResult.IndexName,
            RecordId = validationResult.RecordId,
            ExpectedDimensions = validationResult.ExpectedDimensions,
            ActualDimensions = validationResult.ActualDimensions,
            ProcessedCount = 0,
            AttemptedCount = attemptedCount,
            FailedCount = attemptedCount,
            SupportsPartialSuccess = false,
        };
    }

    private static UpsertVectorResult CreateMappingFailureResult(string indexName, int attemptedCount)
    {
        return new UpsertVectorResult
        {
            Succeeded = false,
            ErrorCode = VectorStoreUpsertErrorCode.MappingFailed,
            Reason = MappingFailedReason,
            IndexName = indexName,
            ProcessedCount = 0,
            AttemptedCount = attemptedCount,
            FailedCount = attemptedCount,
            SupportsPartialSuccess = false,
        };
    }

    private static UpsertVectorResult CreateStoreFailureResult(UpsertVectorRequest request)
    {
        return new UpsertVectorResult
        {
            Succeeded = false,
            ErrorCode = VectorStoreUpsertErrorCode.StoreFailed,
            Reason = StoreFailedReason,
            IndexName = request.IndexName,
            ProcessedCount = 0,
            AttemptedCount = request.Records.Count,
            FailedCount = request.Records.Count,
            SupportsPartialSuccess = false,
        };
    }
}
