using Runiq.AI.Rag.Abstractions.Chunking;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Ingestion;

namespace Runiq.AI.Rag.Services;

/// <summary>
/// Runs the default document ingestion flow by chunking a document and generating embeddings for the resulting chunks.
/// </summary>
public sealed class DefaultRagDocumentIngestionService : IRagDocumentIngestionService
{
    private readonly IRagChunker chunker;
    private readonly IRagChunkEmbeddingGenerator chunkEmbeddingGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRagDocumentIngestionService"/> class.
    /// </summary>
    /// <param name="chunker">The document chunker used to split the document into ordered chunks.</param>
    /// <param name="chunkEmbeddingGenerator">The provider-neutral generator used to embed each chunk.</param>
    public DefaultRagDocumentIngestionService(
        IRagChunker chunker,
        IRagChunkEmbeddingGenerator chunkEmbeddingGenerator)
    {
        this.chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        this.chunkEmbeddingGenerator = chunkEmbeddingGenerator ?? throw new ArgumentNullException(nameof(chunkEmbeddingGenerator));
    }

    /// <summary>
    /// Chunks the document, embeds the chunk content, and returns ordered chunk-to-embedding associations.
    /// </summary>
    /// <param name="document">The source document to ingest.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>The ordered ingestion result for the source document.</returns>
    public async Task<RagDocumentIngestionResult> IngestAsync(
        RagDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<RagChunk> chunks;

        try
        {
            chunks = await chunker.ChunkAsync(document, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The RAG chunker returned null.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"RAG document ingestion failed while chunking document '{document.Id}'.",
                exception);
        }

        if (chunks.Count == 0)
        {
            return new RagDocumentIngestionResult
            {
                DocumentId = document.Id,
                Chunks = Array.Empty<RagChunk>(),
                Items = Array.Empty<RagDocumentIngestionItem>(),
            };
        }

        IReadOnlyList<RagChunkEmbeddingResult> embeddingResults;

        try
        {
            embeddingResults = await chunkEmbeddingGenerator.GenerateAsync(chunks, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The RAG chunk embedding generator returned null.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"RAG document ingestion failed while generating chunk embeddings for document '{document.Id}'.",
                exception);
        }

        var items = AssociateChunksWithEmbeddings(document.Id, chunks, embeddingResults);

        return new RagDocumentIngestionResult
        {
            DocumentId = document.Id,
            Chunks = chunks,
            Items = items,
        };
    }

    private static IReadOnlyList<RagDocumentIngestionItem> AssociateChunksWithEmbeddings(
        string documentId,
        IReadOnlyList<RagChunk> chunks,
        IReadOnlyList<RagChunkEmbeddingResult> embeddingResults)
    {
        if (embeddingResults.Count != chunks.Count)
        {
            throw new InvalidOperationException(
                $"RAG document ingestion expected {chunks.Count} embedding results for document '{documentId}' but received {embeddingResults.Count}.");
        }

        var items = new List<RagDocumentIngestionItem>(chunks.Count);

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index] ?? throw new InvalidOperationException(
                $"RAG document ingestion failed at chunk index {index} because the chunk is null.");
            var embeddingResult = embeddingResults[index] ?? throw new InvalidOperationException(
                $"RAG document ingestion failed at chunk index {index} because the embedding result is null.");

            if (!string.Equals(chunk.Id, embeddingResult.ChunkId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"RAG document ingestion expected embedding result for chunk '{chunk.Id}' at index {index} but received '{embeddingResult.ChunkId}'.");
            }

            if (!string.Equals(chunk.DocumentId, embeddingResult.DocumentId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"RAG document ingestion expected embedding result for document '{chunk.DocumentId}' at chunk index {index} but received '{embeddingResult.DocumentId}'.");
            }

            items.Add(new RagDocumentIngestionItem
            {
                Chunk = chunk,
                EmbeddingResult = embeddingResult,
            });
        }

        return items;
    }
}

