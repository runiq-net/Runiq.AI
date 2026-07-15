using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.Abstractions.VectorStores;

/// <summary>
/// Defines provider-independent validation for vector record dimensions before records are written to a vector store.
/// </summary>
public interface IRagVectorRecordDimensionValidator
{
    /// <summary>
    /// Validates that every vector record in an upsert request matches the expected vector index dimension count.
    /// </summary>
    /// <param name="request">The provider-independent upsert request to validate.</param>
    /// <param name="expectedDimensions">The dimension count expected by the target vector index.</param>
    /// <returns>The provider-independent dimension validation result.</returns>
    VectorRecordDimensionValidationResult Validate(
        UpsertVectorRequest request,
        int expectedDimensions);

    /// <summary>
    /// Validates that every vector record in an upsert request matches the expected vector index dimension count.
    /// </summary>
    /// <param name="request">The provider-independent upsert request to validate.</param>
    /// <param name="expectedDimensions">The dimension count expected by the target vector index.</param>
    /// <param name="cancellationToken">A token that can be used to cancel validation before the provider write is attempted.</param>
    /// <returns>The provider-independent dimension validation result.</returns>
    ValueTask<VectorRecordDimensionValidationResult> ValidateAsync(
        UpsertVectorRequest request,
        int expectedDimensions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(Validate(request, expectedDimensions));
    }
}

