using Runiq.AI.Rag.Models.Context;
using Runiq.AI.Rag.Models.Queries;

namespace Runiq.AI.Rag.Abstractions.Services;

/// <summary>
/// Represents the high-level RAG service facade.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Gets assembled RAG context for the specified query.
    /// </summary>
    /// <param name="query">The query used to assemble context.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The assembled RAG context.</returns>
    Task<RagContext> GetContextAsync(
        RagQuery query,
        CancellationToken cancellationToken = default);
}

