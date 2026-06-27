using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;

namespace Runiq.Rag.Abstractions.Retrieval;

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
}
