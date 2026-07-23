using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.Models.VectorStores;

/// <summary>
/// Carries a provider-independent lexical query. Surrounding double quotes express exact phrase intent.
/// </summary>
public sealed class QueryLexicalRequest
{
    /// <summary>Gets or initializes the target index name.</summary>
    public required string IndexName { get; init; }

    /// <summary>Gets or initializes the lexical query text.</summary>
    public required string QueryText { get; init; }

    /// <summary>Gets or initializes the maximum number of candidates.</summary>
    public int TopK { get; init; } = 5;

    /// <summary>Gets or initializes the metadata filter.</summary>
    public RetrievalMetadataFilter MetadataFilter { get; init; } = RetrievalMetadataFilter.Empty;
}
