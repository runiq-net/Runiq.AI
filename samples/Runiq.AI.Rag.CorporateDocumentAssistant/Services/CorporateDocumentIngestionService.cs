using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.CorporateDocumentAssistant.Models;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.CorporateDocumentAssistant.Services;

/// <summary>
/// Coordinates sample document ingestion through the public RAG ingestion and vector-store upsert APIs.
/// </summary>
public sealed class CorporateDocumentIngestionService
{
    private readonly IRagDocumentIngestionService ingestionService;
    private readonly IRagVectorStore vectorStore;
    private readonly IRagVectorStoreUpsertPipeline upsertPipeline;
    private readonly CorporateDocumentAssistantOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorporateDocumentIngestionService"/> class.
    /// </summary>
    /// <param name="ingestionService">The RAG service that chunks documents and creates embeddings.</param>
    /// <param name="vectorStore">The configured vector store used by the sample.</param>
    /// <param name="upsertPipeline">The provider-independent pipeline that writes embedded chunks to the vector store.</param>
    /// <param name="options">The sample options that provide the target vector index name.</param>
    public CorporateDocumentIngestionService(
        IRagDocumentIngestionService ingestionService,
        IRagVectorStore vectorStore,
        IRagVectorStoreUpsertPipeline upsertPipeline,
        IOptions<CorporateDocumentAssistantOptions> options)
    {
        this.ingestionService = ingestionService ?? throw new ArgumentNullException(nameof(ingestionService));
        this.vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        this.upsertPipeline = upsertPipeline ?? throw new ArgumentNullException(nameof(upsertPipeline));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Ingests one plain-text document, generates deterministic sample embeddings, and upserts the chunks into the configured index.
    /// </summary>
    /// <param name="request">The document content and source information to ingest.</param>
    /// <param name="cancellationToken">A token that can cancel the ingestion and upsert flow.</param>
    /// <returns>A response containing chunk, embedding, and upsert counts for the ingested document.</returns>
    public async Task<CorporateDocumentIngestionResponse> IngestAsync(
        CorporateDocumentIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Document content is required.", nameof(request));
        }

        var documentId = string.IsNullOrWhiteSpace(request.Id)
            ? $"document-{Guid.NewGuid():N}"
            : request.Id.Trim();

        var sourceName = string.IsNullOrWhiteSpace(request.Title)
            ? documentId
            : request.Title.Trim();

        var metadata = new RagDocumentMetadata
        {
            SourceId = documentId,
            SourceName = sourceName,
            SourceUri = $"sample://corporate-documents/{documentId}",
            ContentType = sourceName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? "text/markdown"
                : "text/plain",
            AdditionalMetadata = new RagMetadata(new Dictionary<string, string>
            {
                ["sample"] = "true",
                ["scenario"] = "corporate-document-assistant",
            }),
        };

        var document = new RagDocument
        {
            Id = documentId,
            Content = request.Content,
            Metadata = metadata,
        };

        await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

        var ingestionResult = await ingestionService.IngestAsync(document, cancellationToken).ConfigureAwait(false);
        var upsertResult = await upsertPipeline.UpsertAsync(
            ingestionResult,
            options.IndexName,
            metadata,
            DeterministicCorporateEmbeddingProvider.Dimensions,
            cancellationToken).ConfigureAwait(false);

        return new CorporateDocumentIngestionResponse
        {
            DocumentId = documentId,
            SourceName = sourceName,
            IndexName = options.IndexName,
            ChunkCount = ingestionResult.Chunks.Count,
            EmbeddingCount = ingestionResult.Items.Count,
            UpsertSucceeded = upsertResult.Succeeded,
            UpsertedCount = upsertResult.ProcessedCount,
            VectorIds = upsertResult.VectorIds.ToArray(),
        };
    }

    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        var result = await vectorStore.CreateIndexAsync(
            new CreateVectorIndexRequest
            {
                IndexName = options.IndexName,
                Dimensions = DeterministicCorporateEmbeddingProvider.Dimensions,
            },
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not create vector index '{options.IndexName}': {result.Reason}");
        }
    }
}

