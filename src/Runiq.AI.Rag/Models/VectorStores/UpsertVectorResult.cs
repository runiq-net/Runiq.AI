using Runiq.AI.Rag.Models.Metadata;

namespace Runiq.AI.Rag.Models.VectorStores;

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
    /// Gets or initializes the provider-independent error category for this result. The default value is
    /// <see cref="VectorStoreUpsertErrorCode.None"/>, which this model does not tie to <see cref="Succeeded"/>
    /// on its own. Results produced by the Vector Store upsert pipeline carry a stronger guarantee: the pipeline
    /// normalizes every failure — dimension validation failures, mapping failures, exceptions raised by the
    /// vector store, and vector store results that report <see cref="Succeeded"/> as <see langword="false"/>
    /// without throwing — into a non-<see cref="VectorStoreUpsertErrorCode.None"/> error code, and leaves this
    /// value as <see cref="VectorStoreUpsertErrorCode.None"/> only on successful results.
    /// </summary>
    public VectorStoreUpsertErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Gets or initializes the total number of vector records the pipeline attempted to upsert for this
    /// operation, regardless of whether the operation succeeded or failed.
    /// </summary>
    public int AttemptedCount { get; init; }

    /// <summary>
    /// Gets or initializes the number of vector records that failed to upsert. The default value is zero.
    /// Because the Vector Store upsert pipeline does not support partial success, in every result the pipeline
    /// produces this value equals <see cref="AttemptedCount"/> when <see cref="Succeeded"/> is
    /// <see langword="false"/>, and is zero when <see cref="Succeeded"/> is <see langword="true"/>.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether this result can represent a batch in which some vector
    /// records succeeded while others failed. The default Vector Store upsert pipeline does not support partial
    /// success — an upsert either fully succeeds for every attempted record or is reported as a full failure —
    /// so this value is <see langword="false"/> for every result the pipeline produces.
    /// </summary>
    public bool SupportsPartialSuccess { get; init; }

    /// <summary>
    /// Gets or initializes provider-independent result metadata that describes the upsert outcome without exposing provider-specific exception types.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}

