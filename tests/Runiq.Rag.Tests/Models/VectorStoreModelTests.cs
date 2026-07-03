using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.VectorStores;

namespace Runiq.Rag.Tests.Models;

public sealed class VectorStoreModelTests
{
    [Fact]
    public void CreateVectorIndexRequest_DefaultMetadata_ShouldNotBeNull()
    {
        var request = new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 1536,
        };

        Assert.NotNull(request.Metadata);
        Assert.Equal(VectorDistanceMetric.Cosine, request.Metric);
    }

    [Fact]
    public void UpsertVectorRequest_ShouldHoldMultipleVectorRecords()
    {
        var records = new List<VectorRecord>
        {
            new()
            {
                Id = "vector-1",
                Values = [0.1f, 0.2f],
            },
            new()
            {
                Id = "vector-2",
                Values = [0.3f, 0.4f],
            },
        };

        var request = new UpsertVectorRequest
        {
            IndexName = "documents",
            ExpectedDimensions = 2,
            Records = records,
        };

        records.Clear();

        Assert.Equal("documents", request.IndexName);
        Assert.Equal(2, request.ExpectedDimensions);
        Assert.Equal(2, request.Records.Count);
        Assert.Equal(["vector-1", "vector-2"], request.Records.Select(record => record.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpsertVectorRequest_ShouldFailFast_WhenIndexNameIsInvalid(string? indexName)
    {
        var exception = Assert.Throws<ArgumentException>(() => new UpsertVectorRequest
        {
            IndexName = indexName!,
            Records =
            [
                new VectorRecord
                {
                    Id = "vector-1",
                    Values = [0.1f, 0.2f],
                },
            ],
        });

        Assert.Equal(nameof(UpsertVectorRequest.IndexName), exception.ParamName);
    }

    [Fact]
    public void UpsertVectorRequest_ShouldNormalizeNullRecordsToEmptyCollection()
    {
        var request = new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = null!,
        };

        Assert.NotNull(request.Records);
        Assert.Empty(request.Records);
    }

    [Fact]
    public void UpsertVectorRequest_ShouldAllowEmptyRecordsCollection()
    {
        var request = new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [],
        };

        Assert.NotNull(request.Records);
        Assert.Empty(request.Records);
    }

    [Fact]
    public void VectorRecord_ShouldCarryIdValuesAndMetadata()
    {
        var record = new VectorRecord
        {
            Id = "vector-1",
            Values = [0.1f, 0.2f],
            Content = "content",
            Metadata = new RagMetadata(new Dictionary<string, string>
            {
                ["source"] = "docs",
            }),
        };

        Assert.Equal("vector-1", record.Id);
        Assert.Equal([0.1f, 0.2f], record.Values);
        Assert.Equal("docs", record.Metadata.Values["source"]);
    }

    [Fact]
    public void UpsertVectorResult_ShouldCarryOperationOutcome()
    {
        var result = new UpsertVectorResult
        {
            Succeeded = true,
            UpsertedCount = 2,
            VectorIds = ["vector-1", "vector-2"],
        };

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.ProcessedCount);
        Assert.Equal(2, result.UpsertedCount);
        Assert.Equal(["vector-1", "vector-2"], result.VectorIds);
    }

    [Fact]
    public void UpsertVectorResult_ShouldCarryFailedOperationOutcome()
    {
        var result = new UpsertVectorResult
        {
            Succeeded = false,
            ProcessedCount = 1,
            Reason = "provider-independent failure",
            IndexName = "documents",
            RecordId = "vector-1",
            ExpectedDimensions = 3,
            ActualDimensions = 2,
        };

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.UpsertedCount);
        Assert.Equal("provider-independent failure", result.Reason);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(2, result.ActualDimensions);
    }

    [Fact]
    public void UpsertVectorResult_DefaultErrorDiagnostics_ShouldIndicateNoErrorAndNoPartialSuccessSupport()
    {
        var result = new UpsertVectorResult();

        Assert.Equal(VectorStoreUpsertErrorCode.None, result.ErrorCode);
        Assert.Equal(0, result.AttemptedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(result.SupportsPartialSuccess);
    }

    [Fact]
    public void UpsertVectorResult_ShouldCarryProviderIndependentErrorDiagnostics()
    {
        var result = new UpsertVectorResult
        {
            Succeeded = false,
            ErrorCode = VectorStoreUpsertErrorCode.StoreFailed,
            Reason = "Vector store upsert failed.",
            ProcessedCount = 0,
            AttemptedCount = 3,
            FailedCount = 3,
        };

        Assert.Equal(VectorStoreUpsertErrorCode.StoreFailed, result.ErrorCode);
        Assert.Equal(3, result.AttemptedCount);
        Assert.Equal(3, result.FailedCount);
        Assert.Equal(0, result.ProcessedCount);
        Assert.False(result.SupportsPartialSuccess);
    }

    [Fact]
    public void UpsertVectorResult_ShouldPreserveProcessedCountFromCompatibilityAlias()
    {
        var result = new UpsertVectorResult
        {
            Succeeded = true,
            UpsertedCount = 3,
        };

        Assert.Equal(3, result.ProcessedCount);
        Assert.Equal(3, result.UpsertedCount);
    }

    [Fact]
    public void IRagVectorStore_ShouldExposeProviderIndependentUpsertContract()
    {
        var method = typeof(IRagVectorStore).GetMethod(
            nameof(IRagVectorStore.UpsertAsync),
            [typeof(UpsertVectorRequest), typeof(CancellationToken)]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<UpsertVectorResult>), method.ReturnType);
    }

    [Fact]
    public void QueryVectorRequest_DefaultOptions_ShouldBeProviderIndependent()
    {
        var metadataFilter = new RagMetadata(new Dictionary<string, string>
        {
            ["tenant"] = "runiq",
        });
        var request = new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [0.1f, 0.2f],
            TopK = 3,
            MetadataFilter = metadataFilter,
        };

        Assert.Equal("documents", request.IndexName);
        Assert.Equal([0.1f, 0.2f], request.Values);
        Assert.Equal(3, request.TopK);
        Assert.True(request.IncludeMetadata);
        Assert.False(request.IncludeVectors);
        Assert.Same(metadataFilter, request.MetadataFilter);
        Assert.Equal("runiq", request.MetadataFilter.Values["tenant"]);
        Assert.NotNull(request.Metadata);
    }

    [Fact]
    public void QueryVectorRequest_DefaultTopKAndMetadataFilter_ShouldNotBeNull()
    {
        var request = new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [0.1f, 0.2f],
        };

        Assert.Equal(5, request.TopK);
        Assert.NotNull(request.MetadataFilter);
        Assert.Empty(request.MetadataFilter.Values);
    }

    [Fact]
    public void QueryVectorResult_DefaultRecords_ShouldNotBeNullAndShouldAllowEmptyResults()
    {
        var result = new QueryVectorResult
        {
            Succeeded = true,
        };

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Records);
        Assert.Empty(result.Records);
    }

    [Fact]
    public void QueryVectorResult_ShouldHoldVectorIdScoreAndOptionalMetadata()
    {
        var result = new QueryVectorResult
        {
            Succeeded = true,
        };

        result.Records.Add(new VectorSearchResult
        {
            Id = "vector-1",
            Score = 0.98,
            Content = "content",
            Metadata = new RagMetadata(new Dictionary<string, string>
            {
                ["source"] = "docs",
            }),
            Values = [0.1f, 0.2f],
        });

        var searchResult = Assert.Single(result.Records);
        Assert.Equal("vector-1", searchResult.Id);
        Assert.Equal(0.98, searchResult.Score);
        Assert.Equal("docs", searchResult.Metadata.Values["source"]);
        Assert.Equal([0.1f, 0.2f], searchResult.Values);
    }

    [Fact]
    public void VectorSearchResult_DefaultMetadata_ShouldSupportOptionalMetadata()
    {
        var result = new VectorSearchResult
        {
            Id = "vector-1",
            Score = 0.98,
        };

        Assert.NotNull(result.Metadata);
        Assert.Empty(result.Metadata.Values);
        Assert.Null(result.Values);
    }

    [Fact]
    public void IRagVectorStore_ShouldExposeProviderIndependentQueryContract()
    {
        var method = typeof(IRagVectorStore).GetMethod(
            nameof(IRagVectorStore.QueryAsync),
            [typeof(QueryVectorRequest), typeof(CancellationToken)]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<QueryVectorResult>), method.ReturnType);
    }

    [Fact]
    public void DeleteVectorRequest_ShouldHoldMultipleVectorIds()
    {
        var request = new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = ["vector-1", "vector-2"],
        };

        Assert.NotNull(request.VectorIds);
        Assert.Equal(["vector-1", "vector-2"], request.VectorIds);
        Assert.NotNull(request.Metadata);
    }

    [Fact]
    public void DeleteVectorRequest_ShouldNormalizeNullVectorIdsToEmptyCollection()
    {
        var request = new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = null!,
        };

        Assert.NotNull(request.VectorIds);
        Assert.Empty(request.VectorIds);
    }

    [Fact]
    public void DeleteVectorResult_ShouldCarrySuccessfulOperationOutcome()
    {
        var result = new DeleteVectorResult
        {
            Succeeded = true,
            RequestedCount = 2,
            DeletedCount = 2,
            VectorIds = ["vector-1", "vector-2"],
        };

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(["vector-1", "vector-2"], result.VectorIds);
        Assert.Empty(result.NotFoundVectorIds);
    }

    [Fact]
    public void DeleteVectorResult_ShouldCarryFailedOperationOutcome()
    {
        var result = new DeleteVectorResult
        {
            Succeeded = false,
            RequestedCount = 2,
            DeletedCount = 0,
            NotFoundVectorIds = ["vector-1", "vector-2"],
            Reason = "delete failed",
        };

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(["vector-1", "vector-2"], result.NotFoundVectorIds);
        Assert.Equal("delete failed", result.Reason);
    }

    [Fact]
    public void DeleteVectorResult_ShouldCarryDeterministicNotFoundVectorIds()
    {
        var result = new DeleteVectorResult
        {
            Succeeded = true,
            RequestedCount = 3,
            DeletedCount = 2,
            VectorIds = ["vector-1", "vector-3"],
            NotFoundVectorIds = ["vector-2"],
        };

        Assert.Equal(3, result.RequestedCount);
        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(["vector-2"], result.NotFoundVectorIds);
    }

    [Fact]
    public void IRagVectorStore_ShouldExposeProviderIndependentDeleteContract()
    {
        var method = typeof(IRagVectorStore).GetMethod(
            nameof(IRagVectorStore.DeleteAsync),
            [typeof(DeleteVectorRequest), typeof(CancellationToken)]);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<DeleteVectorResult>), method.ReturnType);
    }

    [Fact]
    public void OperationResults_DefaultMetadataAndReason_ShouldNotBeNull()
    {
        var createResult = new CreateVectorIndexResult
        {
            IndexName = "documents",
        };
        var upsertResult = new UpsertVectorResult();
        var deleteResult = new DeleteVectorResult();

        Assert.NotNull(createResult.Metadata);
        Assert.Equal(string.Empty, createResult.Reason);
        Assert.NotNull(upsertResult.Metadata);
        Assert.NotNull(upsertResult.VectorIds);
        Assert.Equal(string.Empty, upsertResult.Reason);
        Assert.NotNull(deleteResult.Metadata);
        Assert.NotNull(deleteResult.VectorIds);
        Assert.NotNull(deleteResult.NotFoundVectorIds);
        Assert.Equal(string.Empty, deleteResult.Reason);
    }
}
