namespace Runiq.AI.Rag.Tests.Retrieval.Integration.Support;

/// <summary>
/// A readable description of a single record to seed into a vector index for a retrieval integration test.
/// It pairs a stable record id with the chunk content that will be embedded and the provider-independent
/// metadata that retrieval assertions and metadata filters run against. Vectors are intentionally not carried
/// here: the seeding harness embeds <see cref="Content"/> through the deterministic embedding so query text
/// and stored content always share the same vector space.
/// </summary>
public sealed class RetrievalSeedRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalSeedRecord"/> class.
    /// </summary>
    /// <param name="id">The stable record identifier, typically a chunk id.</param>
    /// <param name="content">The chunk content that is embedded and later asserted on.</param>
    /// <param name="metadata">Optional provider-independent metadata used by assertions and metadata filters.</param>
    public RetrievalSeedRecord(
        string id,
        string content,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Gets the stable record identifier used both as the vector record id and to correlate retrieval matches.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the chunk content that is embedded during seeding and returned as the retrieval match content.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Gets the provider-independent metadata stored with the record for retrieval and metadata-filter assertions.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }
}

