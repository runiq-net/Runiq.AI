using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Represents a request to delete vector records from a vector index.
/// </summary>
public sealed class DeleteVectorRequest
{
    private IList<string> vectorIds = new List<string>();
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteVectorRequest"/> class.
    /// </summary>
    public DeleteVectorRequest()
    {
    }

    /// <summary>
    /// Gets or initializes the vector index name.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// Gets or initializes the identifiers to delete.
    /// </summary>
    public IList<string> VectorIds
    {
        get => vectorIds;
        init => vectorIds = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes request metadata.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
