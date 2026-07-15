using Runiq.AI.Rag.Models.Documents;

namespace Runiq.AI.Rag.Abstractions.Chunking;

/// <summary>
/// Defines a service that splits RAG documents into reusable chunks.
/// </summary>
public interface IRagChunker
{
    /// <summary>
    /// Splits the specified document into ordered RAG chunks.
    /// </summary>
    /// <param name="document">The source document to split into chunks.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The ordered chunks extracted from the document.</returns>
    Task<IReadOnlyList<RagChunk>> ChunkAsync(
        RagDocument document,
        CancellationToken cancellationToken = default);
}

