using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Retrieval;
using Runiq.Rag.Models.Search;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.Retrieval;

namespace Runiq.Rag.VectorStores.InMemory;

/// <summary>
/// Provides a deterministic in-memory vector store intended for development, tests, and samples — not production use.
/// Upsert operations are all-or-nothing: every record in a request is validated before any record is written, so a
/// failed batch never leaves partial state. The upsert <see cref="CancellationToken"/> is observed before any state
/// is mutated. Stored vector values and metadata are defensively copied, so later mutation of the source request does
/// not affect stored records. Dimension validation is not performed here; it remains the responsibility of
/// <see cref="ValidatingRagVectorStore"/>.
/// </summary>
public sealed class InMemoryRagVectorStore : IRagVectorStore
{
    private const string IndexNotFoundReason = "Vector index has not been created.";
    private const string DimensionMismatchReason = "Vector dimension does not match the index dimensions.";
    private const string RequestRequiredReason = "Request is required.";
    private const string InvalidIndexNameReason = "Vector index name is required.";
    private const string InvalidDimensionsReason = "Vector dimensions must be greater than zero.";
    private const string RecordsRequiredReason = "At least one vector record is required.";
    private const string InvalidVectorIdReason = "Vector identifier is required.";
    private const string InvalidVectorValuesReason = "Vector values are required.";
    private const string InvalidTopKReason = "TopK must be greater than zero.";
    private const string UnsupportedFilterOperatorReason = "Metadata filter operator is not supported.";

    private readonly object gate = new();
    private readonly Dictionary<string, InMemoryVectorIndex> indexes = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryRagVectorStore"/> class.
    /// </summary>
    public InMemoryRagVectorStore()
    {
    }

    /// <inheritdoc />
    public Task<CreateVectorIndexResult> CreateIndexAsync(
        CreateVectorIndexRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Task.FromResult(CreateFailedIndexResult(string.Empty, RequestRequiredReason));
        }

        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return Task.FromResult(CreateFailedIndexResult(request.IndexName ?? string.Empty, InvalidIndexNameReason));
        }

        if (request.Dimensions <= 0)
        {
            return Task.FromResult(CreateFailedIndexResult(request.IndexName, InvalidDimensionsReason));
        }

        lock (gate)
        {
            if (indexes.TryGetValue(request.IndexName, out var existingIndex))
            {
                if (existingIndex.Dimensions != request.Dimensions)
                {
                    return Task.FromResult(new CreateVectorIndexResult
                    {
                        IndexName = request.IndexName,
                        Succeeded = false,
                        Reason = DimensionMismatchReason,
                    });
                }

                return Task.FromResult(new CreateVectorIndexResult
                {
                    IndexName = request.IndexName,
                    Succeeded = true,
                });
            }

            indexes[request.IndexName] = new InMemoryVectorIndex(request.Dimensions, request.Metric);
        }

        return Task.FromResult(new CreateVectorIndexResult
        {
            IndexName = request.IndexName,
            Succeeded = true,
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// A null request is a programming error and throws <see cref="ArgumentNullException"/>. The cancellation token
    /// is observed before validation and before any record is written; a cancelled token therefore never leaves
    /// partial state. All records are validated before the first write, so a failed batch is a full failure with
    /// <see cref="UpsertVectorResult.ProcessedCount"/> of zero.
    /// </remarks>
    public Task<UpsertVectorResult> UpsertAsync(
        UpsertVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var attemptedCount = request.Records?.Count ?? 0;

        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return Task.FromResult(CreateFailedUpsertResult(
                VectorStoreUpsertErrorCode.ValidationFailed, InvalidIndexNameReason, attemptedCount));
        }

        if (request.Records is null || request.Records.Count == 0)
        {
            return Task.FromResult(CreateFailedUpsertResult(
                VectorStoreUpsertErrorCode.ValidationFailed, RecordsRequiredReason, attemptedCount));
        }

        foreach (var record in request.Records)
        {
            if (record is null || string.IsNullOrWhiteSpace(record.Id))
            {
                return Task.FromResult(CreateFailedUpsertResult(
                    VectorStoreUpsertErrorCode.ValidationFailed, InvalidVectorIdReason, attemptedCount));
            }

            if (record.Values is null || record.Values.Count == 0)
            {
                return Task.FromResult(CreateFailedUpsertResult(
                    VectorStoreUpsertErrorCode.ValidationFailed, InvalidVectorValuesReason, attemptedCount));
            }
        }

        lock (gate)
        {
            if (!indexes.TryGetValue(request.IndexName, out var index))
            {
                return Task.FromResult(CreateFailedUpsertResult(
                    VectorStoreUpsertErrorCode.StoreFailed, IndexNotFoundReason, attemptedCount));
            }

            foreach (var record in request.Records)
            {
                index.Records[record.Id] = CopyRecord(record);
            }

            return Task.FromResult(new UpsertVectorResult
            {
                Succeeded = true,
                ErrorCode = VectorStoreUpsertErrorCode.None,
                ProcessedCount = request.Records.Count,
                AttemptedCount = request.Records.Count,
                FailedCount = 0,
                SupportsPartialSuccess = false,
                VectorIds = request.Records.Select(record => record.Id).ToList(),
            });
        }
    }

    /// <inheritdoc />
    public Task<DeleteVectorResult> DeleteAsync(
        DeleteVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Task.FromResult(CreateFailedDeleteResult(0, [], RequestRequiredReason));
        }

        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return Task.FromResult(CreateFailedDeleteResult(request.VectorIds?.Count ?? 0, request.VectorIds ?? [], InvalidIndexNameReason));
        }

        if (request.VectorIds is null || request.VectorIds.Count == 0)
        {
            return Task.FromResult(new DeleteVectorResult
            {
                Succeeded = true,
                RequestedCount = 0,
            });
        }

        if (request.VectorIds.Any(string.IsNullOrWhiteSpace))
        {
            return Task.FromResult(CreateFailedDeleteResult(request.VectorIds.Count, request.VectorIds, InvalidVectorIdReason));
        }

        lock (gate)
        {
            if (!indexes.TryGetValue(request.IndexName, out var index))
            {
                return Task.FromResult(CreateFailedDeleteResult(request.VectorIds.Count, request.VectorIds, IndexNotFoundReason));
            }

            var deletedIds = new List<string>();
            var notFoundIds = new List<string>();

            foreach (var vectorId in request.VectorIds)
            {
                if (index.Records.Remove(vectorId))
                {
                    deletedIds.Add(vectorId);
                }
                else
                {
                    notFoundIds.Add(vectorId);
                }
            }

            return Task.FromResult(new DeleteVectorResult
            {
                Succeeded = true,
                RequestedCount = request.VectorIds.Count,
                DeletedCount = deletedIds.Count,
                VectorIds = deletedIds,
                NotFoundVectorIds = notFoundIds,
            });
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A null request is reported through the returned result model rather than throwing. The cancellation token is
    /// observed before any index state is read, so a cancelled token fails fast with
    /// <see cref="OperationCanceledException"/>. The query is isolated to the requested index, evaluates only the
    /// records under that index, applies the provider-independent metadata filter before similarity scoring so
    /// filtered-out records never enter scoring, orders the remaining matches best score first, and returns at most
    /// <see cref="QueryVectorRequest.TopK"/> results. An empty or fully filtered index is a successful, empty result.
    /// A filter carrying an operator this store does not support fails deterministically with a failure result.
    /// Query state is read-only, so existing upsert behavior is never affected.
    /// </remarks>
    public Task<QueryVectorResult> QueryAsync(
        QueryVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Task.FromResult(CreateFailedQueryResult(RequestRequiredReason));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return Task.FromResult(CreateFailedQueryResult(InvalidIndexNameReason));
        }

        if (request.Values is null || request.Values.Count == 0)
        {
            return Task.FromResult(CreateFailedQueryResult(InvalidVectorValuesReason));
        }

        if (request.TopK <= 0)
        {
            return Task.FromResult(CreateFailedQueryResult(InvalidTopKReason));
        }

        if (HasUnsupportedFilterOperator(request.MetadataFilter))
        {
            return Task.FromResult(CreateFailedQueryResult(UnsupportedFilterOperatorReason));
        }

        lock (gate)
        {
            if (!indexes.TryGetValue(request.IndexName, out var index))
            {
                return Task.FromResult(CreateFailedQueryResult(IndexNotFoundReason));
            }

            if (request.Values.Count != index.Dimensions)
            {
                return Task.FromResult(CreateFailedQueryResult(DimensionMismatchReason));
            }

            var records = index.Records.Values
                    .Where(record => MatchesMetadataFilter(record, request.MetadataFilter))
                    .Select(record => new VectorSearchResult
                    {
                        Id = record.Id,
                        Content = record.Content,
                        Metadata = request.IncludeMetadata ? CopyMetadata(record.Metadata) : RagMetadata.Empty,
                        Score = CalculateScore(index.Metric, request.Values, record.Values),
                        Values = request.IncludeVectors ? record.Values.ToArray() : null,
                    })
                    .OrderByDescending(record => record.Score)
                    .ThenBy(record => record.Id, StringComparer.Ordinal)
                    .Take(request.TopK)
                    .ToList();

            return Task.FromResult(new QueryVectorResult
            {
                Succeeded = true,
                Records = records,
            });
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The cancellation token is observed before the chunk is converted into an upsert request and again by the
    /// request-based overload before any record is written, so a cancelled token never mutates store state.
    /// </remarks>
    public Task<UpsertVectorResult> UpsertAsync(
        string indexName,
        RagChunk chunk,
        RagEmbedding embedding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(embedding);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(indexName))
        {
            return Task.FromResult(CreateFailedUpsertResult(
                VectorStoreUpsertErrorCode.ValidationFailed, InvalidIndexNameReason, attemptedCount: 1));
        }

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
    public async Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        RagQuery query,
        RagEmbedding embedding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(embedding);

        var result = await QueryAsync(
            new QueryVectorRequest
            {
                IndexName = query.IndexName ?? string.Empty,
                Values = embedding.Values,
                TopK = query.TopK,
                MetadataFilter = new RetrievalMetadataFilter(query.Metadata.Values),
                Metadata = query.Metadata,
                IncludeMetadata = true,
            },
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new RagVectorStoreQueryException(
                result.Reason,
                query.IndexName ?? string.Empty);
        }

        return RagSearchResultMapper.Map(result.Records);
    }

    private static VectorRecord CopyRecord(VectorRecord record)
    {
        return new VectorRecord
        {
            Id = record.Id,
            Values = record.Values.ToArray(),
            Content = record.Content,
            Metadata = CopyMetadata(record.Metadata),
        };
    }

    private static RagMetadata CopyMetadata(RagMetadata metadata)
    {
        return new RagMetadata(metadata.Values);
    }

    /// <summary>
    /// Determines whether the store supports every operator carried by the filter. Operator support is a
    /// per-store decision; this in-memory store currently supports only exact-match equality, and any other
    /// operator value is reported as a deterministic query failure rather than being silently ignored.
    /// </summary>
    private static bool HasUnsupportedFilterOperator(RetrievalMetadataFilter metadataFilter)
    {
        return metadataFilter.Criteria.Any(
            criterion => criterion.Operator != RetrievalMetadataFilterOperator.Equal);
    }

    /// <summary>
    /// Applies the provider-independent metadata filter to a stored record with logical AND semantics: every
    /// criterion must match for the record to enter similarity scoring. An empty filter retains every record.
    /// </summary>
    private static bool MatchesMetadataFilter(VectorRecord record, RetrievalMetadataFilter metadataFilter)
    {
        if (metadataFilter.IsEmpty)
        {
            return true;
        }

        return metadataFilter.Criteria.All(criterion => MatchesCriterion(record.Metadata, criterion));
    }

    /// <summary>
    /// Evaluates a single criterion against record metadata using deterministic ordinal comparison. A record
    /// that does not carry the criterion key never matches.
    /// </summary>
    private static bool MatchesCriterion(RagMetadata metadata, RetrievalMetadataFilterCriterion criterion)
    {
        return criterion.Operator switch
        {
            RetrievalMetadataFilterOperator.Equal =>
                metadata.Values.TryGetValue(criterion.Key, out var value) &&
                StringComparer.Ordinal.Equals(value, criterion.Value),
            _ => false,
        };
    }

    private static CreateVectorIndexResult CreateFailedIndexResult(string indexName, string reason)
    {
        return new CreateVectorIndexResult
        {
            IndexName = indexName,
            Succeeded = false,
            Reason = reason,
        };
    }

    /// <summary>
    /// Builds a full-failure upsert result that satisfies the standard upsert contract: no record is reported as
    /// processed, every attempted record is reported as failed, and partial success is never indicated.
    /// </summary>
    private static UpsertVectorResult CreateFailedUpsertResult(
        VectorStoreUpsertErrorCode errorCode,
        string reason,
        int attemptedCount)
    {
        return new UpsertVectorResult
        {
            Succeeded = false,
            ErrorCode = errorCode,
            Reason = reason,
            ProcessedCount = 0,
            AttemptedCount = attemptedCount,
            FailedCount = attemptedCount,
            SupportsPartialSuccess = false,
        };
    }

    private static QueryVectorResult CreateFailedQueryResult(string reason)
    {
        return new QueryVectorResult
        {
            Succeeded = false,
            Reason = reason,
        };
    }

    private static DeleteVectorResult CreateFailedDeleteResult(
        int requestedCount,
        IEnumerable<string?> notFoundVectorIds,
        string reason)
    {
        return new DeleteVectorResult
        {
            Succeeded = false,
            RequestedCount = requestedCount,
            NotFoundVectorIds = notFoundVectorIds.Where(id => id is not null).Select(id => id!).ToList(),
            Reason = reason,
        };
    }

    private static double CalculateScore(
        VectorDistanceMetric metric,
        IReadOnlyList<float> queryValues,
        IReadOnlyList<float> recordValues)
    {
        return metric switch
        {
            VectorDistanceMetric.DotProduct => DotProduct(queryValues, recordValues),
            VectorDistanceMetric.Euclidean => 1.0 / (1.0 + EuclideanDistance(queryValues, recordValues)),
            _ => CosineSimilarity(queryValues, recordValues),
        };
    }

    private static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var dotProduct = 0.0;
        var leftMagnitude = 0.0;
        var rightMagnitude = 0.0;

        for (var i = 0; i < left.Count; i++)
        {
            dotProduct += left[i] * right[i];
            leftMagnitude += left[i] * left[i];
            rightMagnitude += right[i] * right[i];
        }

        if (leftMagnitude == 0.0 || rightMagnitude == 0.0)
        {
            return 0.0;
        }

        return dotProduct / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private static double DotProduct(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var dotProduct = 0.0;

        for (var i = 0; i < left.Count; i++)
        {
            dotProduct += left[i] * right[i];
        }

        return dotProduct;
    }

    private static double EuclideanDistance(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var sum = 0.0;

        for (var i = 0; i < left.Count; i++)
        {
            var difference = left[i] - right[i];
            sum += difference * difference;
        }

        return Math.Sqrt(sum);
    }

    private static RagMetadata BuildChunkMetadata(RagChunk chunk)
    {
        var values = new Dictionary<string, string>(chunk.Metadata.AdditionalMetadata.Values)
        {
            ["documentId"] = chunk.DocumentId,
            ["chunkIndex"] = chunk.Index.ToString(),
        };

        if (chunk.Metadata.StartIndex.HasValue)
        {
            values["startIndex"] = chunk.Metadata.StartIndex.Value.ToString();
        }

        if (chunk.Metadata.EndIndex.HasValue)
        {
            values["endIndex"] = chunk.Metadata.EndIndex.Value.ToString();
        }

        if (chunk.Metadata.TokenCount.HasValue)
        {
            values["tokenCount"] = chunk.Metadata.TokenCount.Value.ToString();
        }

        return new RagMetadata(values);
    }

    private sealed class InMemoryVectorIndex
    {
        public InMemoryVectorIndex(int dimensions, VectorDistanceMetric metric)
        {
            Dimensions = dimensions;
            Metric = metric;
        }

        public int Dimensions { get; }

        public VectorDistanceMetric Metric { get; }

        public Dictionary<string, VectorRecord> Records { get; } = new(StringComparer.Ordinal);
    }
}
