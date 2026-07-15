using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.VectorStores;

namespace Runiq.AI.Rag.Tests.VectorStores;

public sealed class DefaultRagVectorRecordDimensionValidatorTests
{
    [Fact]
    public void Validate_ShouldSucceed_WhenVectorRecordMatchesExpectedDimensions()
    {
        var validator = new DefaultRagVectorRecordDimensionValidator();

        var result = validator.Validate(CreateRequest(CreateRecord("vector-1", [0.1f, 0.2f, 0.3f])), 3);

        Assert.True(result.Succeeded);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(string.Empty, result.Reason);
    }

    [Fact]
    public void Validate_ShouldFail_WhenVectorRecordHasFewerDimensionsThanExpected()
    {
        var validator = new DefaultRagVectorRecordDimensionValidator();

        var result = validator.Validate(CreateRequest(CreateRecord("vector-1", [0.1f, 0.2f])), 3);

        Assert.False(result.Succeeded);
        Assert.Equal("Vector dimension does not match the index dimensions.", result.Reason);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(2, result.ActualDimensions);
    }

    [Fact]
    public void Validate_ShouldFail_WhenVectorRecordHasMoreDimensionsThanExpected()
    {
        var validator = new DefaultRagVectorRecordDimensionValidator();

        var result = validator.Validate(CreateRequest(CreateRecord("vector-1", [0.1f, 0.2f, 0.3f, 0.4f])), 3);

        Assert.False(result.Succeeded);
        Assert.Equal("Vector dimension does not match the index dimensions.", result.Reason);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(4, result.ActualDimensions);
    }

    [Fact]
    public void Validate_ShouldFailDeterministically_WhenVectorValuesAreNull()
    {
        var validator = new DefaultRagVectorRecordDimensionValidator();

        var result = validator.Validate(CreateRequest(CreateRecord("vector-1", null!)), 3);

        Assert.False(result.Succeeded);
        Assert.Equal("Vector values are required.", result.Reason);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Null(result.ActualDimensions);
    }

    [Fact]
    public void Validate_ShouldFailDeterministically_WhenVectorValuesAreEmpty()
    {
        var validator = new DefaultRagVectorRecordDimensionValidator();

        var result = validator.Validate(CreateRequest(CreateRecord("vector-1", [])), 3);

        Assert.False(result.Succeeded);
        Assert.Equal("Vector values are required.", result.Reason);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(0, result.ActualDimensions);
    }

    [Fact]
    public void Validate_ShouldCheckEveryRecordInMultiRecordRequest()
    {
        var validator = new DefaultRagVectorRecordDimensionValidator();

        var result = validator.Validate(
            CreateRequest(
                CreateRecord("valid-vector", [0.1f, 0.2f, 0.3f]),
                CreateRecord("invalid-vector", [0.1f, 0.2f])),
            3);

        Assert.False(result.Succeeded);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("invalid-vector", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(2, result.ActualDimensions);
    }

    private static UpsertVectorRequest CreateRequest(params VectorRecord[] records)
    {
        return new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = records,
        };
    }

    private static VectorRecord CreateRecord(string id, IReadOnlyList<float> values)
    {
        return new VectorRecord
        {
            Id = id,
            Values = values,
        };
    }
}

