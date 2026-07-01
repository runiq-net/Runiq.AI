using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.VectorStores;

namespace Runiq.Rag.VectorStores;

/// <summary>
/// Validates vector record dimensions using only provider-independent upsert request data and expected index configuration.
/// </summary>
public sealed class DefaultRagVectorRecordDimensionValidator : IRagVectorRecordDimensionValidator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRagVectorRecordDimensionValidator"/> class.
    /// </summary>
    public DefaultRagVectorRecordDimensionValidator()
    {
    }

    /// <inheritdoc />
    public VectorRecordDimensionValidationResult Validate(
        UpsertVectorRequest request,
        int expectedDimensions)
    {
        if (request is null)
        {
            return CreateFailedResult(
                string.Empty,
                string.Empty,
                expectedDimensions,
                actualDimensions: null,
                "Request is required.");
        }

        if (expectedDimensions <= 0)
        {
            return CreateFailedResult(
                request.IndexName,
                string.Empty,
                expectedDimensions,
                actualDimensions: null,
                "Vector dimensions must be greater than zero.");
        }

        foreach (var record in request.Records)
        {
            if (record is null)
            {
                return CreateFailedResult(
                    request.IndexName,
                    string.Empty,
                    expectedDimensions,
                    actualDimensions: null,
                    "Vector record is required.");
            }

            if (record.Values is null)
            {
                return CreateFailedResult(
                    request.IndexName,
                    record.Id,
                    expectedDimensions,
                    actualDimensions: null,
                    "Vector values are required.");
            }

            var actualDimensions = record.Values.Count;

            if (actualDimensions == 0)
            {
                return CreateFailedResult(
                    request.IndexName,
                    record.Id,
                    expectedDimensions,
                    actualDimensions,
                    "Vector values are required.");
            }

            if (actualDimensions != expectedDimensions)
            {
                return CreateFailedResult(
                    request.IndexName,
                    record.Id,
                    expectedDimensions,
                    actualDimensions,
                    "Vector dimension does not match the index dimensions.");
            }
        }

        return new VectorRecordDimensionValidationResult
        {
            Succeeded = true,
            IndexName = request.IndexName,
            ExpectedDimensions = expectedDimensions,
        };
    }

    /// <inheritdoc />
    public ValueTask<VectorRecordDimensionValidationResult> ValidateAsync(
        UpsertVectorRequest request,
        int expectedDimensions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(Validate(request, expectedDimensions));
    }

    private static VectorRecordDimensionValidationResult CreateFailedResult(
        string indexName,
        string recordId,
        int expectedDimensions,
        int? actualDimensions,
        string reason)
    {
        return new VectorRecordDimensionValidationResult
        {
            Succeeded = false,
            Reason = reason,
            IndexName = indexName,
            RecordId = recordId,
            ExpectedDimensions = expectedDimensions,
            ActualDimensions = actualDimensions,
        };
    }
}
