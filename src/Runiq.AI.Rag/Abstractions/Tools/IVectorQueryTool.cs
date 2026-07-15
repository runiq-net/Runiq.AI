using Runiq.AI.Rag.Models.Tools;

namespace Runiq.AI.Rag.Abstractions.Tools;

/// <summary>
/// Represents the provider-independent Vector Query Tool an agent can invoke to run retrieval against a
/// configured vector store and index. The contract is intentionally thin: it accepts a
/// <see cref="VectorQueryToolRequest"/> carrying the vector store name, index name, embedding model identifier,
/// user query text, top-k value, and metadata filter, and returns a <see cref="VectorQueryToolResult"/> in an
/// agent-usable shape. Implementations delegate execution to the existing RAG retrieval flow and must not know
/// any concrete embedding provider or vector store implementation, introduce a new retrieval pipeline, or own
/// provider selection.
/// </summary>
public interface IVectorQueryTool
{
    /// <summary>
    /// Executes the tool by adapting the request to the existing retrieval flow and mapping the retrieval
    /// outcome into an agent-usable result. A semantically invalid request — for example a missing index name,
    /// an empty query, or a non-positive top-k value — is reported deterministically as a failed
    /// <see cref="VectorQueryToolResult"/> rather than as a provider-specific exception.
    /// </summary>
    /// <param name="request">The provider-independent Vector Query Tool request.</param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the operation. Implementations must forward it through the retrieval
    /// call chain.
    /// </param>
    /// <returns>
    /// A successful result carrying the retrieved matches with their content, metadata, and similarity scores,
    /// or a failed result with a provider-independent error category.
    /// </returns>
    Task<VectorQueryToolResult> ExecuteAsync(
        VectorQueryToolRequest request,
        CancellationToken cancellationToken = default);
}

