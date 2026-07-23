using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Rag.Models.Retrieval;

/// <summary>Combines ordered retrieval candidates with source and fusion candidate statistics.</summary>
public sealed record RagRetrievalExecutionResult
{
    /// <summary>Gets the ordered candidates returned after final limiting.</summary>
    public required IReadOnlyList<RagSearchResult> Candidates { get; init; }

    /// <summary>Gets source and fusion counts captured before final limiting.</summary>
    public required RagRetrievalStatistics Statistics { get; init; }
}
