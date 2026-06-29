using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Represents a vector record that can be persisted in a vector store.
/// </summary>
public sealed class VectorRecord
{
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorRecord"/> class.
    /// </summary>
    public VectorRecord()
    {
    }

    /// <summary>
    /// Gets or initializes the vector identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or initializes the vector values.
    /// </summary>
    public required IReadOnlyList<float> Values { get; init; }

    /// <summary>
    /// Gets or initializes the content associated with the vector.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes record metadata.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
