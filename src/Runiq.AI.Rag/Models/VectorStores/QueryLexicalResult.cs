using Runiq.AI.Rag.Models.Metadata;

namespace Runiq.AI.Rag.Models.VectorStores;

/// <summary>Represents the outcome of a lexical provider query.</summary>
public sealed class QueryLexicalResult
{
    /// <summary>Gets or initializes whether the query succeeded.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Gets or initializes the provider-independent failure reason.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Gets or initializes matches ordered by lexical rank.</summary>
    public IReadOnlyList<VectorSearchResult> Records { get; init; } = Array.Empty<VectorSearchResult>();

    /// <summary>Gets or initializes result metadata.</summary>
    public RagMetadata Metadata { get; init; } = RagMetadata.Empty;
}
