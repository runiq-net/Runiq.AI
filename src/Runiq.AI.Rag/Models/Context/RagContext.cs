using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Rag.Models.Context;

/// <summary>
/// Represents assembled context that can later be passed to an agent or prompt builder.
/// </summary>
public sealed class RagContext
{
    private IList<RagSearchResult> results = new List<RagSearchResult>();
    private RagMetadata metadata = RagMetadata.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagContext"/> class.
    /// </summary>
    public RagContext()
    {
    }

    /// <summary>
    /// Gets or initializes the query used to assemble the context.
    /// </summary>
    public required RagQuery Query { get; init; }

    /// <summary>
    /// Gets or initializes the retrieved search results included in the context.
    /// </summary>
    public IList<RagSearchResult> Results
    {
        get => results;
        init => results = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or initializes the assembled context content.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes context metadata.
    /// </summary>
    public RagMetadata Metadata
    {
        get => metadata;
        init => metadata = value ?? throw new ArgumentNullException(nameof(value));
    }
}

