using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.VectorStores;
using System.Globalization;

namespace Runiq.AI.Rag.VectorStores;

/// <summary>
/// Wraps a provider vector store with provider-independent validation that runs before write operations reach the provider.
/// </summary>
public sealed class ValidatingRagVectorStore : IRagVectorStore
{
    private const string ExpectedDimensionsRequiredReason = "Vector expected dimensions are required for upsert validation.";
    private const string ExpectedDimensionsConflictReason = "Vector expected dimensions conflict with the cached index dimensions.";

    private readonly object gate = new();
    private readonly IRagVectorStore innerVectorStore;
    private readonly IRagVectorRecordDimensionValidator dimensionValidator;
    private readonly Dictionary<string, int> indexDimensions = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatingRagVectorStore"/> class.
    /// </summary>
    /// <param name="innerVectorStore">The provider vector store that receives validated operations.</param>
    /// <param name="dimensionValidator">The provider-independent vector record dimension validator.</param>
    public ValidatingRagVectorStore(
        IRagVectorStore innerVectorStore,
        IRagVectorRecordDimensionValidator dimensionValidator)
    {
        this.innerVectorStore = innerVectorStore ?? throw new ArgumentNullException(nameof(innerVectorStore));
        this.dimensionValidator = dimensionValidator ?? throw new ArgumentNullException(nameof(dimensionValidator));
    }

    /// <inheritdoc />
    public async Task<CreateVectorIndexResult> CreateIndexAsync(
        CreateVectorIndexRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await innerVectorStore.CreateIndexAsync(request, cancellationToken).ConfigureAwait(false);

        if (request is not null && result.Succeeded && !string.IsNullOrWhiteSpace(request.IndexName) && request.Dimensions > 0)
        {
            lock (gate)
            {
                indexDimensions[request.IndexName] = request.Dimensions;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<UpsertVectorResult> UpsertAsync(
        UpsertVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return await innerVectorStore.UpsertAsync(request!, cancellationToken).ConfigureAwait(false);
        }

        var expectedDimensionsResult = ResolveExpectedDimensions(request);
        if (!expectedDimensionsResult.Succeeded)
        {
            return CreateFailedUpsertResult(expectedDimensionsResult, request.Records.Count);
        }

        var validationResult = await dimensionValidator.ValidateAsync(
            request,
            expectedDimensionsResult.ExpectedDimensions,
            cancellationToken).ConfigureAwait(false);

        if (!validationResult.Succeeded)
        {
            return CreateFailedUpsertResult(validationResult, request.Records.Count);
        }

        return await innerVectorStore.UpsertAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<DeleteVectorResult> DeleteAsync(
        DeleteVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        return innerVectorStore.DeleteAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<QueryVectorResult> QueryAsync(
        QueryVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        return innerVectorStore.QueryAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<UpsertVectorResult> UpsertAsync(
        string indexName,
        RagChunk chunk,
        RagEmbedding embedding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(embedding);

        return UpsertAsync(
            new UpsertVectorRequest
            {
                IndexName = indexName,
                Records =
                [
                    new VectorRecord
                    {
                        Id = chunk.Id,
                        Values = embedding.Values,
                        Content = chunk.Content,
                        Metadata = BuildChunkMetadata(chunk),
                    },
                ],
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        RagQuery query,
        RagEmbedding embedding,
        CancellationToken cancellationToken = default)
    {
        return innerVectorStore.SearchAsync(query, embedding, cancellationToken);
    }

    private bool TryGetExpectedDimensions(string indexName, out int expectedDimensions)
    {
        lock (gate)
        {
            return indexDimensions.TryGetValue(indexName, out expectedDimensions);
        }
    }

    private VectorRecordDimensionValidationResult ResolveExpectedDimensions(UpsertVectorRequest request)
    {
        var hasCachedDimensions = TryGetExpectedDimensions(request.IndexName, out var cachedDimensions);

        if (request.ExpectedDimensions.HasValue)
        {
            if (hasCachedDimensions && cachedDimensions != request.ExpectedDimensions.Value)
            {
                return CreateExpectedDimensionsFailure(
                    request,
                    request.ExpectedDimensions.Value,
                    ExpectedDimensionsConflictReason);
            }

            return new VectorRecordDimensionValidationResult
            {
                Succeeded = true,
                IndexName = request.IndexName,
                ExpectedDimensions = request.ExpectedDimensions.Value,
            };
        }

        if (hasCachedDimensions)
        {
            return new VectorRecordDimensionValidationResult
            {
                Succeeded = true,
                IndexName = request.IndexName,
                ExpectedDimensions = cachedDimensions,
            };
        }

        return CreateExpectedDimensionsFailure(
            request,
            expectedDimensions: 0,
            ExpectedDimensionsRequiredReason);
    }

    private static VectorRecordDimensionValidationResult CreateExpectedDimensionsFailure(
        UpsertVectorRequest request,
        int expectedDimensions,
        string reason)
    {
        var record = request.Records.FirstOrDefault();

        return new VectorRecordDimensionValidationResult
        {
            Succeeded = false,
            Reason = reason,
            IndexName = request.IndexName,
            RecordId = record?.Id ?? string.Empty,
            ExpectedDimensions = expectedDimensions,
            ActualDimensions = record?.Values?.Count,
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
        };
    }

    private static Models.Metadata.RagMetadata BuildChunkMetadata(RagChunk chunk)
    {
        var values = new Dictionary<string, string>(chunk.Metadata.AdditionalMetadata.Values)
        {
            ["documentId"] = chunk.DocumentId,
            ["chunkIndex"] = chunk.Index.ToString(CultureInfo.InvariantCulture),
        };

        if (chunk.Metadata.StartIndex.HasValue)
        {
            values["startIndex"] = chunk.Metadata.StartIndex.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (chunk.Metadata.EndIndex.HasValue)
        {
            values["endIndex"] = chunk.Metadata.EndIndex.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (chunk.Metadata.TokenCount.HasValue)
        {
            values["tokenCount"] = chunk.Metadata.TokenCount.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new Models.Metadata.RagMetadata(values);
    }
}

