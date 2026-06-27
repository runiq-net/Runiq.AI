using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Metadata;

namespace Runiq.Rag.Models.Search;

/// <summary>
/// Represents a retrieved chunk match.
/// </summary>
public sealed class RagSearchResult
{
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagSearchResult"/> class.
    /// </summary>
    public RagSearchResult()
    {
    }

    /// <summary>
    /// Gets or initializes the matched chunk.
    /// </summary>
    public required RagChunk Chunk { get; init; }

    /// <summary>
    /// Gets or initializes the relevance or similarity score.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Gets or initializes search result metadata.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}
