using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Represents the result of inserting or updating vector records.
/// </summary>
public sealed class UpsertVectorResult
{
    private IList<string> vectorIds = new List<string>();
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertVectorResult"/> class.
    /// </summary>
    public UpsertVectorResult()
    {
    }

    /// <summary>
    /// Gets or initializes a value indicating whether the operation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets or initializes the number of records inserted or updated.
    /// </summary>
    public int UpsertedCount { get; init; }

    /// <summary>
    /// Gets or initializes the identifiers affected by the operation.
    /// </summary>
    public IList<string> VectorIds
    {
        get => vectorIds;
        init => vectorIds = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes a provider-independent failure reason when the operation did not succeed.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes result metadata.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
