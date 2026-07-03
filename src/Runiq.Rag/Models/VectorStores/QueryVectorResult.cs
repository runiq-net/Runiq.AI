using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Represents the provider-independent result of a vector similarity query. A query that matches no records is still
/// a success: <see cref="Succeeded"/> is <see langword="true"/> with an empty <see cref="Records"/> collection.
/// Invalid requests or store failures are reported with <see cref="Succeeded"/> set to <see langword="false"/> and a
/// populated <see cref="Reason"/>.
/// </summary>
public sealed class QueryVectorResult
{
    private IList<VectorSearchResult> records = new List<VectorSearchResult>();
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryVectorResult"/> class.
    /// </summary>
    public QueryVectorResult()
    {
    }

    /// <summary>
    /// Gets or initializes the matching vector records ordered best match first. The collection is empty when the
    /// query matched no records.
    /// </summary>
    public IList<VectorSearchResult> Records
    {
        get => records;
        init => records = value ?? throw new ArgumentNullException(nameof(value));
    }

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
