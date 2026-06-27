using Runiq.Rag.Abstractions.Retrieval;
using Runiq.Rag.Abstractions.Services;
using Runiq.Rag.Models.Context;
using Runiq.Rag.Models.Queries;

namespace Runiq.Rag.Services;

/// <summary>
/// Provides the default high-level RAG service orchestration.
/// </summary>
public sealed class RagService : IRagService
{
    private readonly IRagRetriever retriever;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagService"/> class.
    /// </summary>
    /// <param name="retriever">The retriever used to retrieve relevant RAG search results.</param>
    public RagService(IRagRetriever retriever)
    {
        this.retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
    }

    /// <summary>
    /// Gets assembled RAG context for the specified query.
    /// </summary>
    /// <param name="query">The query used to assemble context.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The assembled RAG context.</returns>
    public async Task<RagContext> GetContextAsync(
        RagQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = await retriever.RetrieveAsync(query, cancellationToken).ConfigureAwait(false);
        var content = results.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, results.Select(result => result.Chunk.Content));

        return new RagContext
        {
            Query = query,
            Results = results.ToList(),
            Content = content,
        };
    }
}
