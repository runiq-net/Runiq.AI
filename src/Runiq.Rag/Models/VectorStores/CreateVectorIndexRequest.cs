using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Represents a request to create a provider-independent vector index.
/// </summary>
public sealed class CreateVectorIndexRequest
{
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateVectorIndexRequest"/> class.
    /// </summary>
    public CreateVectorIndexRequest()
    {
    }

    /// <summary>
    /// Gets or initializes the vector index name.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// Gets or initializes the vector dimension count expected by the index.
    /// </summary>
    public required int Dimensions { get; init; }

    /// <summary>
    /// Gets or initializes the distance or similarity metric used by the index.
    /// </summary>
    public VectorDistanceMetric Metric { get; init; } = VectorDistanceMetric.Cosine;

    /// <summary>
    /// Gets or initializes request metadata.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
