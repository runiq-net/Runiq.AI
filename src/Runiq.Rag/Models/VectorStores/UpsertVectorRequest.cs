using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Represents a request to insert or update vector records in a vector index.
/// </summary>
public sealed class UpsertVectorRequest
{
    private RagMetadata metadata = RagMetadata.Empty;
    private IList<VectorRecord> records = new List<VectorRecord>();

    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertVectorRequest"/> class.
    /// </summary>
    public UpsertVectorRequest()
    {
    }

    /// <summary>
    /// Gets or initializes the target vector index name.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// Gets or initializes the vector records to insert or update.
    /// </summary>
    public IList<VectorRecord> Records
    {
        get => records;
        init => records = value ?? new List<VectorRecord>();
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
