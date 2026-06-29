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
        var request = new UpsertVectorRequest
        {
            IndexName = "documents",
            Records =
            [
                new VectorRecord
                {
                    Id = "vector-1",
                    Values = [0.1f, 0.2f],
                },
                new VectorRecord
                {
                    Id = "vector-2",
                    Values = [0.3f, 0.4f],
                },
            ],
        };

        Assert.Equal(2, request.Records.Count);
        Assert.Equal(["vector-1", "vector-2"], request.Records.Select(record => record.Id));
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
        Assert.Equal(2, result.UpsertedCount);
        Assert.Equal(["vector-1", "vector-2"], result.VectorIds);
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
        var request = new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [0.1f, 0.2f],
        };

        Assert.Equal(5, request.TopK);
        Assert.True(request.IncludeMetadata);
        Assert.False(request.IncludeVectors);
        Assert.NotNull(request.MetadataFilter);
        Assert.NotNull(request.Metadata);
    }

    [Fact]
    public void QueryVectorResult_DefaultRecords_ShouldNotBeNullAndShouldHoldResults()
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
            Values = [0.1f, 0.2f],
        });

        Assert.NotNull(result.Records);
        Assert.Single(result.Records);
        Assert.NotNull(result.Records[0].Metadata);
    }

    [Fact]
    public void DeleteVectorRequest_DefaultVectorIdsAndMetadataFilter_ShouldNotBeNull()
    {
        var request = new DeleteVectorRequest
        {
            IndexName = "documents",
        };

        request.VectorIds.Add("vector-1");

        Assert.NotNull(request.VectorIds);
        Assert.Single(request.VectorIds);
        Assert.NotNull(request.MetadataFilter);
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
        Assert.Equal(string.Empty, deleteResult.Reason);
    }
}
