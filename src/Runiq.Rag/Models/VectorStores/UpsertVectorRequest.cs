using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Carries provider-independent data required by the Vector Store upsert pipeline to insert or update records in a vector index.
/// </summary>
public sealed class UpsertVectorRequest
{
    private string indexName = null!;
    private RagMetadata metadata = RagMetadata.Empty;
    private IList<VectorRecord> records = new List<VectorRecord>();

    /// <summary>
    /// Initializes a new instance of the <see cref="UpsertVectorRequest"/> class.
    /// </summary>
    public UpsertVectorRequest()
    {
    }

    /// <summary>
    /// Gets or initializes the target vector index that should receive the supplied vector records.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the index name is null, empty, or contains only whitespace.
    /// </exception>
    public required string IndexName
    {
        get => indexName;
        init => indexName = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Vector index name is required.", nameof(IndexName))
            : value;
    }

    /// <summary>
    /// Gets or initializes the vector records that the upsert pipeline should insert or update in the target index.
    /// A null collection is normalized to an empty collection so providers can deterministically decide whether
    /// an empty upsert batch is valid for their implementation.
    /// </summary>
    public IList<VectorRecord> Records
    {
        get => records;
        init => records = value?.ToList() ?? new List<VectorRecord>();
    }

    /// <summary>
    /// Gets or initializes provider-independent request metadata that can flow through the upsert pipeline without binding to a specific vector store provider.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
