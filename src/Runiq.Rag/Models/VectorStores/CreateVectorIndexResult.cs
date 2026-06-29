using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Represents the result of a provider-independent vector index creation operation.
/// </summary>
public sealed class CreateVectorIndexResult
{
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateVectorIndexResult"/> class.
    /// </summary>
    public CreateVectorIndexResult()
    {
    }

    /// <summary>
    /// Gets or initializes the vector index name.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether the operation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

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
