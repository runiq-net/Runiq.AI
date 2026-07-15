namespace Runiq.AI.Rag.Models.VectorStores;

/// <summary>
/// Carries the provider-independent outcome of vector record dimension validation.
/// </summary>
public sealed class VectorRecordDimensionValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorRecordDimensionValidationResult"/> class.
    /// </summary>
    public VectorRecordDimensionValidationResult()
    {
    }

    /// <summary>
    /// Gets or initializes a value indicating whether the validated vector records match the expected dimension count.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets or initializes the provider-independent failure reason when dimension validation did not succeed.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the vector index name associated with the validation result.
    /// </summary>
    public string IndexName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the vector record identifier associated with a validation failure.
    /// </summary>
    public string RecordId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the dimension count expected by the target vector index.
    /// </summary>
    public int ExpectedDimensions { get; init; }

    /// <summary>
    /// Gets or initializes the actual dimension count calculated from the vector record values when available.
    /// </summary>
    public int? ActualDimensions { get; init; }
}

