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
public sealed class DefaultRagVectorStoreUpsertPipeline : IRagVectorStoreUpsertPipeline
{
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
        cancellationToken.ThrowIfCancellationRequested();

        var mappedRequest = upsertVectorRequestMapper.Map(ingestionResult, indexName, documentMetadata);

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
                return CreateFailedUpsertResult(validationResult);
            }
        }

        return await vectorStore.UpsertAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static UpsertVectorResult CreateFailedUpsertResult(VectorRecordDimensionValidationResult validationResult)
    {
        return new UpsertVectorResult
        {
            Succeeded = false,
            Reason = validationResult.Reason,
            IndexName = validationResult.IndexName,
            RecordId = validationResult.RecordId,
            ExpectedDimensions = validationResult.ExpectedDimensions,
            ActualDimensions = validationResult.ActualDimensions,
        };
    }
}
