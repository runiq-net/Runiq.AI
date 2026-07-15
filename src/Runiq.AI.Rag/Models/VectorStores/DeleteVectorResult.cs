using Runiq.AI.Rag.Models.Metadata;

namespace Runiq.AI.Rag.Models.VectorStores;

/// <summary>
/// Represents the result of deleting vector records from a vector index.
/// </summary>
public sealed class DeleteVectorResult
{
    private IList<string> vectorIds = new List<string>();
    private IList<string> notFoundVectorIds = new List<string>();
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteVectorResult"/> class.
    /// </summary>
    public DeleteVectorResult()
    {
    }

    /// <summary>
    /// Gets or initializes a value indicating whether the operation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets or initializes the number of vector identifiers requested for deletion.
    /// </summary>
    public int RequestedCount { get; init; }

    /// <summary>
    /// Gets or initializes the number of records deleted.
    /// </summary>
    public int DeletedCount { get; init; }

    /// <summary>
    /// Gets or initializes the identifiers deleted by the operation.
    /// </summary>
    public IList<string> VectorIds
    {
        get => vectorIds;
        init => vectorIds = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes the identifiers that were requested but not found.
    /// </summary>
    public IList<string> NotFoundVectorIds
    {
        get => notFoundVectorIds;
        init => notFoundVectorIds = value ?? throw new ArgumentNullException(nameof(value));
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

