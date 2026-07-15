namespace Runiq.AI.Rag.Chunking;

/// <summary>
/// Provides provider-independent options for RAG document chunking.
/// </summary>
public sealed class RagChunkingOptions
{
    /// <summary>
    /// Gets or sets the maximum number of characters in a generated chunk.
    /// </summary>
    public int MaxChunkLength { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the number of characters that adjacent chunks should overlap.
    /// </summary>
    public int ChunkOverlap { get; set; } = 100;
}

