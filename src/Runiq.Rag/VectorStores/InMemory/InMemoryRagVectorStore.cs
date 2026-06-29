using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;
using Runiq.Rag.Models.VectorStores;

namespace Runiq.Rag.VectorStores.InMemory;

/// <summary>
/// Provides a deterministic in-memory vector store for tests and samples.
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

    private readonly object gate = new();
    private readonly Dictionary<string, InMemoryVectorIndex> indexes = new(StringComparer.Ordinal);

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
    public Task<UpsertVectorResult> UpsertAsync(
        UpsertVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Task.FromResult(CreateFailedUpsertResult(RequestRequiredReason));
        }

        if (string.IsNullOrWhiteSpace(request.IndexName))
        {
            return Task.FromResult(CreateFailedUpsertResult(InvalidIndexNameReason));
        }

        if (request.Records is null || request.Records.Count == 0)
        {
            return Task.FromResult(CreateFailedUpsertResult(RecordsRequiredReason));
        }

        foreach (var record in request.Records)
        {
            if (record is null || string.IsNullOrWhiteSpace(record.Id))
            {
                return Task.FromResult(CreateFailedUpsertResult(InvalidVectorIdReason));
            }

            if (record.Values is null || record.Values.Count == 0)
            {
                return Task.FromResult(CreateFailedUpsertResult(InvalidVectorValuesReason));
            }
        }

        lock (gate)
        {
            if (!indexes.TryGetValue(request.IndexName, out var index))
            {
                return Task.FromResult(CreateFailedUpsertResult(IndexNotFoundReason));
            }

            var dimensionMismatch = request.Records.Any(record => record.Values.Count != index.Dimensions);
            if (dimensionMismatch)
            {
                return Task.FromResult(CreateFailedUpsertResult(DimensionMismatchReason));
            }

            foreach (var record in request.Records)
            {
                index.Records[record.Id] = CopyRecord(record);
            }

            return Task.FromResult(new UpsertVectorResult
            {
                Succeeded = true,
                UpsertedCount = request.Records.Count,
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
    public Task<QueryVectorResult> QueryAsync(
        QueryVectorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Task.FromResult(CreateFailedQueryResult(RequestRequiredReason));
        }

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
    public Task<UpsertVectorResult> UpsertAsync(
        RagChunk chunk,
        RagEmbedding embedding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(embedding);

        return UpsertAsync(
            new UpsertVectorRequest
            {
                IndexName = string.Empty,
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

        var indexName = query.Metadata.Values.TryGetValue("indexName", out var configuredIndexName)
            ? configuredIndexName
            : string.Empty;

        var result = await QueryAsync(
            new QueryVectorRequest
            {
                IndexName = indexName,
                Values = embedding.Values,
                TopK = query.TopK,
                IncludeMetadata = true,
            },
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return Array.Empty<RagSearchResult>();
        }

        return result.Records
            .Select(record => new RagSearchResult
            {
                Chunk = new RagChunk
                {
                    Id = record.Id,
                    DocumentId = GetMetadataValue(record.Metadata, "documentId"),
                    Content = record.Content,
                    Index = ParseInt32(GetMetadataValue(record.Metadata, "chunkIndex")),
                    Metadata = new RagChunkMetadata
                    {
                        AdditionalMetadata = CopyMetadata(record.Metadata),
                    },
                },
                Score = record.Score,
                Metadata = CopyMetadata(record.Metadata),
            })
            .ToList();
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

    private static bool MatchesMetadataFilter(VectorRecord record, RagMetadata metadataFilter)
    {
        if (metadataFilter.Values.Count == 0)
        {
            return true;
        }

        if (record.Metadata.Values.Count == 0)
        {
            return false;
        }

        foreach (var filter in metadataFilter.Values)
        {
            if (!record.Metadata.Values.TryGetValue(filter.Key, out var value) ||
                !StringComparer.Ordinal.Equals(value, filter.Value))
            {
                return false;
            }
        }

        return true;
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

    private static UpsertVectorResult CreateFailedUpsertResult(string reason)
    {
        return new UpsertVectorResult
        {
            Succeeded = false,
            Reason = reason,
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

    private static int ParseInt32(string? value)
    {
        return int.TryParse(value, out var result) ? result : 0;
    }

    private static string GetMetadataValue(RagMetadata metadata, string key)
    {
        return metadata.Values.TryGetValue(key, out var value) ? value : string.Empty;
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
