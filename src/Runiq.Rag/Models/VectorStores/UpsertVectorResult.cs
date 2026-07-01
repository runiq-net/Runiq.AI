using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Carries the provider-independent outcome of a Vector Store upsert pipeline operation.
/// </summary>
public sealed class UpsertVectorResult
{
    private int processedCount;
    private IList<string> vectorIds = new List<string>();
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertVectorResult"/> class.
    /// </summary>
    public UpsertVectorResult()
    {
    }

    /// <summary>
    /// Gets or initializes a value indicating whether the provider-independent upsert operation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets or initializes the number of vector records processed by the upsert operation, regardless of provider implementation details.
    /// </summary>
    public int ProcessedCount
    {
        get => processedCount;
        init => processedCount = value;
    }

    /// <summary>
    /// Gets or initializes the number of vector records inserted or updated. This property is kept as a compatibility alias for <see cref="ProcessedCount" />.
    /// </summary>
    public int UpsertedCount
    {
        get => processedCount;
        init => processedCount = value;
    }

    /// <summary>
    /// Gets or initializes the vector identifiers affected by the provider-independent upsert operation.
    /// </summary>
    public IList<string> VectorIds
    {
        get => vectorIds;
        init => vectorIds = value?.ToList() ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes a provider-independent failure reason when the upsert operation did not succeed.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the vector index name associated with an upsert failure when available.
    /// </summary>
    public string IndexName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the vector record identifier associated with an upsert failure when available.
    /// </summary>
    public string RecordId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the dimension count expected by the target vector index when dimension validation fails.
    /// </summary>
    public int? ExpectedDimensions { get; init; }

    /// <summary>
    /// Gets or initializes the actual dimension count calculated from the vector record values when dimension validation fails.
    /// </summary>
    public int? ActualDimensions { get; init; }

    /// <summary>
    /// Gets or initializes provider-independent result metadata that describes the upsert outcome without exposing provider-specific exception types.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
