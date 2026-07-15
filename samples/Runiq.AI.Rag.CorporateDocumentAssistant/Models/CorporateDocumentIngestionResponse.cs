namespace Runiq.AI.Rag.CorporateDocumentAssistant.Models;

/// <summary>
/// Describes the result of ingesting one document into the sample vector index.
/// </summary>
public sealed class CorporateDocumentIngestionResponse
{
    /// <summary>
    /// Gets the document identifier assigned to the ingested content.
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// Gets the source title retained with vector record metadata for later citation display.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Gets the vector index that received the embedded chunks.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// Gets the number of chunks produced by the configured RAG chunker.
    /// </summary>
    public int ChunkCount { get; init; }

    /// <summary>
    /// Gets the number of embeddings produced for the document chunks.
    /// </summary>
    public int EmbeddingCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the vector store upsert completed successfully.
    /// </summary>
    public bool UpsertSucceeded { get; init; }

    /// <summary>
    /// Gets the number of vector records processed by the upsert pipeline.
    /// </summary>
    public int UpsertedCount { get; init; }

    /// <summary>
    /// Gets vector identifiers written by the upsert pipeline.
    /// </summary>
    public required IReadOnlyList<string> VectorIds { get; init; }
}

