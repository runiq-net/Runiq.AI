using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Rag.Abstractions.Retrieval;

/// <summary>
/// Represents a retriever that retrieves relevant RAG search results for a query.
/// </summary>
public interface IRagRetriever
{
    /// <summary>
    /// Retrieves relevant RAG search results for the specified query.
    /// </summary>
    /// <param name="query">The retrieval query.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The retrieved RAG search results.</returns>
    Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
        RagQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves ordered candidates together with source and fusion candidate counts.</summary>
    /// <param name="query">The retrieval query.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The candidates and retrieval statistics.</returns>
    async Task<RagRetrievalExecutionResult> RetrieveWithMetadataAsync(
        RagQuery query,
        CancellationToken cancellationToken = default)
    {
        var candidates = await RetrieveAsync(query, cancellationToken).ConfigureAwait(false);
        return new RagRetrievalExecutionResult
        {
            Candidates = candidates,
            Statistics = RagRetrievalStatistics.Empty,
        };
    }
}

