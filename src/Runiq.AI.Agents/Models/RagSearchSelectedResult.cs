namespace Runiq.AI.Agents;

/// <summary>Identifies one document and chunk pair selected as runtime context.</summary>
public sealed class RagSearchSelectedResult
{
    /// <summary>Initializes a selected RAG search result.</summary>
    /// <param name="documentId">The source document identifier.</param>
    /// <param name="chunkId">The selected chunk identifier.</param>
    public RagSearchSelectedResult(string documentId, string chunkId)
    {
        DocumentId = string.IsNullOrWhiteSpace(documentId) ? throw new ArgumentException("Document id cannot be empty.", nameof(documentId)) : documentId;
        ChunkId = string.IsNullOrWhiteSpace(chunkId) ? throw new ArgumentException("Chunk id cannot be empty.", nameof(chunkId)) : chunkId;
    }
    /// <summary>Gets the source document identifier.</summary>
    public string DocumentId { get; }
    /// <summary>Gets the selected chunk identifier.</summary>
    public string ChunkId { get; }
}
