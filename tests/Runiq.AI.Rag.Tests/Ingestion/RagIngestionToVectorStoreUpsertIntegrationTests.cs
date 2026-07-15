using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.Tests.Ingestion;

/// <summary>
/// Integration tests that run the full document → chunk → embedding → vector record → upsert chain
/// through the real dependency injection graph registered by <c>AddRuniqRag</c>, using a deterministic
/// fake embedding provider and the in-memory vector store so no external dependency is involved.
/// </summary>
public sealed class RagIngestionToVectorStoreUpsertIntegrationTests
{
    /// <summary>
    /// The fixed vector dimension count produced by <see cref="DeterministicFakeEmbeddingProvider"/>.
    /// </summary>
    private const int EmbeddingDimensions = 3;

    // Verifies that a single document can flow through chunking, deterministic embedding generation,
    // vector record mapping, and upsert into the in-memory vector store under the expected index,
    // and that the stored record count and vector identifiers match the produced chunks.
    [Fact]
    public async Task IngestAndUpsert_ShouldWriteSingleDocumentEmbeddingsToInMemoryStoreUnderRequestedIndex()
    {
        var embeddingProvider = new DeterministicFakeEmbeddingProvider();
        using var serviceProvider = CreateRagServiceProvider(embeddingProvider);
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var document = CreateDocument("document-1", "abcdefgh");
        await CreateIndexAsync(vectorStore, "documents-index");

        var ingestionResult = await IngestAsync(serviceProvider, document);
        var upsertResult = await UpsertAsync(serviceProvider, ingestionResult, "documents-index", document.Metadata);

        Assert.True(upsertResult.Succeeded);
        Assert.Equal("documents-index", upsertResult.IndexName);
        Assert.Equal(2, upsertResult.ProcessedCount);

        var storedRecords = await QueryAllRecordsAsync(vectorStore, "documents-index");

        Assert.Equal(2, storedRecords.Count);
        Assert.Equal(["document-1:chunk:0", "document-1:chunk:1"], storedRecords.Select(record => record.Id));
        Assert.Equal(["abcd", "efgh"], storedRecords.Select(record => record.Content));
    }

    // Verifies that every chunk embedding produced for a multi-chunk document is written to the same
    // vector index, so a document is never split across indexes by the upsert pipeline.
    [Fact]
    public async Task IngestAndUpsert_ShouldWriteAllChunkEmbeddingsOfMultiChunkDocumentToSameIndex()
    {
        var embeddingProvider = new DeterministicFakeEmbeddingProvider();
        using var serviceProvider = CreateRagServiceProvider(embeddingProvider);
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var document = CreateDocument("document-1", "abcdefghijklmnop");
        await CreateIndexAsync(vectorStore, "tenant-a-index");

        var ingestionResult = await IngestAsync(serviceProvider, document);
        var upsertResult = await UpsertAsync(serviceProvider, ingestionResult, "tenant-a-index", document.Metadata);

        Assert.Equal(4, ingestionResult.Chunks.Count);
        Assert.True(upsertResult.Succeeded);
        Assert.Equal(4, upsertResult.ProcessedCount);

        var storedRecords = await QueryAllRecordsAsync(vectorStore, "tenant-a-index");

        Assert.Equal(4, storedRecords.Count);
        Assert.Equal(
            ingestionResult.Chunks.Select(chunk => chunk.Id).Order(StringComparer.Ordinal),
            storedRecords.Select(record => record.Id));
    }

    // Verifies that the original chunk order survives the full ingestion-to-upsert chain by asserting
    // that each stored vector record carries its chunk index in metadata.
    [Fact]
    public async Task IngestAndUpsert_ShouldPreserveChunkOrderInVectorRecordMetadata()
    {
        var embeddingProvider = new DeterministicFakeEmbeddingProvider();
        using var serviceProvider = CreateRagServiceProvider(embeddingProvider);
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var document = CreateDocument("document-1", "abcdefghijkl");
        await CreateIndexAsync(vectorStore, "documents-index");

        var ingestionResult = await IngestAsync(serviceProvider, document);
        await UpsertAsync(serviceProvider, ingestionResult, "documents-index", document.Metadata);

        var storedRecords = await QueryAllRecordsAsync(vectorStore, "documents-index");

        Assert.Equal(["0", "1", "2"], storedRecords.Select(record => record.Metadata.Values["chunkIndex"]));
    }

    // Verifies that the source document identifier survives the full ingestion-to-upsert chain and is
    // stored in the metadata of every vector record produced for that document.
    [Fact]
    public async Task IngestAndUpsert_ShouldPreserveDocumentIdInVectorRecordMetadata()
    {
        var embeddingProvider = new DeterministicFakeEmbeddingProvider();
        using var serviceProvider = CreateRagServiceProvider(embeddingProvider);
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var document = CreateDocument("handbook-2026", "abcdefgh");
        await CreateIndexAsync(vectorStore, "documents-index");

        var ingestionResult = await IngestAsync(serviceProvider, document);
        await UpsertAsync(serviceProvider, ingestionResult, "documents-index", document.Metadata);

        var storedRecords = await QueryAllRecordsAsync(vectorStore, "documents-index");

        Assert.NotEmpty(storedRecords);
        Assert.All(storedRecords, record => Assert.Equal("handbook-2026", record.Metadata.Values["documentId"]));
    }

    // Verifies that each stored vector record keeps its originating chunk identifier in metadata and
    // that the metadata chunk id matches the vector record id used for the upsert.
    [Fact]
    public async Task IngestAndUpsert_ShouldPreserveChunkIdInVectorRecordMetadata()
    {
        var embeddingProvider = new DeterministicFakeEmbeddingProvider();
        using var serviceProvider = CreateRagServiceProvider(embeddingProvider);
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var document = CreateDocument("document-1", "abcdefgh");
        await CreateIndexAsync(vectorStore, "documents-index");

        var ingestionResult = await IngestAsync(serviceProvider, document);
        await UpsertAsync(serviceProvider, ingestionResult, "documents-index", document.Metadata);

        var storedRecords = await QueryAllRecordsAsync(vectorStore, "documents-index");

        Assert.NotEmpty(storedRecords);
        Assert.All(storedRecords, record => Assert.Equal(record.Id, record.Metadata.Values["chunkId"]));
        Assert.Equal(
            ingestionResult.Chunks.Select(chunk => chunk.Id).Order(StringComparer.Ordinal),
            storedRecords.Select(record => record.Metadata.Values["chunkId"]));
    }

    // Verifies that document-level metadata (source fields and additional key/value metadata) is carried
    // through the document → chunk → vector record chain and stored on every vector record.
    [Fact]
    public async Task IngestAndUpsert_ShouldCarryDocumentMetadataIntoVectorRecordMetadata()
    {
        var embeddingProvider = new DeterministicFakeEmbeddingProvider();
        using var serviceProvider = CreateRagServiceProvider(embeddingProvider);
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var document = CreateDocument(
            "document-1",
            "abcdefgh",
            new RagMetadata(new Dictionary<string, string>
            {
                ["tenant"] = "tenant-1",
                ["category"] = "manual",
            }));
        await CreateIndexAsync(vectorStore, "documents-index");

        var ingestionResult = await IngestAsync(serviceProvider, document);
        await UpsertAsync(serviceProvider, ingestionResult, "documents-index", document.Metadata);

        var storedRecords = await QueryAllRecordsAsync(vectorStore, "documents-index");

        Assert.NotEmpty(storedRecords);
        Assert.All(storedRecords, record =>
        {
            Assert.Equal("source-1", record.Metadata.Values["sourceId"]);
            Assert.Equal("Product handbook", record.Metadata.Values["sourceName"]);
            Assert.Equal("https://example.test/product-handbook", record.Metadata.Values["sourceUri"]);
            Assert.Equal("text/plain", record.Metadata.Values["contentType"]);
            Assert.Equal("tenant-1", record.Metadata.Values["tenant"]);
            Assert.Equal("manual", record.Metadata.Values["category"]);
        });
    }

    // Verifies that the deterministic fake embedding provider is the provider actually used by the flow:
    // it observes exactly the chunk contents, and every stored vector equals the deterministic embedding
    // computed from the stored record content, proving no other embedding source was involved.
    [Fact]
    public async Task IngestAndUpsert_ShouldStoreDeterministicFakeEmbeddingsComputedFromChunkContent()
    {
        var embeddingProvider = new DeterministicFakeEmbeddingProvider();
        using var serviceProvider = CreateRagServiceProvider(embeddingProvider);
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var document = CreateDocument("document-1", "abcdefgh");
        await CreateIndexAsync(vectorStore, "documents-index");

        var ingestionResult = await IngestAsync(serviceProvider, document);
        await UpsertAsync(serviceProvider, ingestionResult, "documents-index", document.Metadata);

        Assert.Equal(["abcd", "efgh"], embeddingProvider.Texts);

        var storedRecords = await QueryAllRecordsAsync(vectorStore, "documents-index");

        Assert.NotEmpty(storedRecords);
        Assert.All(storedRecords, record =>
        {
            Assert.NotNull(record.Values);
            Assert.Equal(
                DeterministicFakeEmbeddingProvider.CreateEmbeddingValues(record.Content),
                record.Values);
        });
    }

    // Verifies that the upsert pipeline blocks the write through the existing dimension validation
    // mechanism when the caller-declared expected dimensions do not match the generated embedding
    // dimensions, and that the in-memory store stays empty after the rejected upsert.
    [Fact]
    public async Task IngestAndUpsert_ShouldBlockUpsertAndLeaveStoreEmpty_WhenExpectedDimensionsMismatch()
    {
        var embeddingProvider = new DeterministicFakeEmbeddingProvider();
        using var serviceProvider = CreateRagServiceProvider(embeddingProvider);
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var document = CreateDocument("document-1", "abcdefgh");
        await CreateIndexAsync(vectorStore, "documents-index");

        var ingestionResult = await IngestAsync(serviceProvider, document);
        var upsertResult = await UpsertAsync(
            serviceProvider,
            ingestionResult,
            "documents-index",
            document.Metadata,
            expectedDimensions: EmbeddingDimensions + 2);

        Assert.False(upsertResult.Succeeded);
        Assert.Equal(VectorStoreUpsertErrorCode.ValidationFailed, upsertResult.ErrorCode);
        Assert.Equal(EmbeddingDimensions + 2, upsertResult.ExpectedDimensions);
        Assert.Equal(EmbeddingDimensions, upsertResult.ActualDimensions);
        Assert.Equal(0, upsertResult.ProcessedCount);
        Assert.Equal(ingestionResult.Items.Count, upsertResult.FailedCount);

        var storedRecords = await QueryAllRecordsAsync(vectorStore, "documents-index");

        Assert.Empty(storedRecords);
    }

    // Verifies that a cancellation requested before the upsert pipeline runs is propagated as an
    // OperationCanceledException and that the cancelled upsert never mutates the in-memory store.
    [Fact]
    public async Task IngestAndUpsert_ShouldPropagateUpsertCancellation_WithoutWritingToStore()
    {
        var embeddingProvider = new DeterministicFakeEmbeddingProvider();
        using var serviceProvider = CreateRagServiceProvider(embeddingProvider);
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var document = CreateDocument("document-1", "abcdefgh");
        await CreateIndexAsync(vectorStore, "documents-index");
        var ingestionResult = await IngestAsync(serviceProvider, document);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        using var scope = serviceProvider.CreateScope();
        var upsertPipeline = scope.ServiceProvider.GetRequiredService<IRagVectorStoreUpsertPipeline>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            upsertPipeline.UpsertAsync(
                ingestionResult,
                "documents-index",
                document.Metadata,
                cancellationToken: cancellationTokenSource.Token));

        var storedRecords = await QueryAllRecordsAsync(vectorStore, "documents-index");

        Assert.Empty(storedRecords);
    }

    // Verifies that a cancellation requested before ingestion starts is propagated through the
    // ingestion service, so no chunking or embedding work runs for a cancelled request.
    [Fact]
    public async Task Ingest_ShouldPropagateCancellation_BeforeChunkingAndEmbedding()
    {
        var embeddingProvider = new DeterministicFakeEmbeddingProvider();
        using var serviceProvider = CreateRagServiceProvider(embeddingProvider);
        var document = CreateDocument("document-1", "abcdefgh");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        using var scope = serviceProvider.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<IRagDocumentIngestionService>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ingestionService.IngestAsync(document, cancellationTokenSource.Token));

        Assert.Empty(embeddingProvider.Texts);
    }

    /// <summary>
    /// Builds the real RAG dependency injection graph through <c>AddRuniqRag</c> with the in-memory
    /// vector store and the supplied deterministic fake embedding provider, so integration tests
    /// exercise the production wiring without any external dependency.
    /// </summary>
    /// <param name="embeddingProvider">The deterministic fake embedding provider used instead of a real provider.</param>
    /// <param name="maxChunkLength">The maximum chunk length applied to the default chunker.</param>
    /// <param name="chunkOverlap">The chunk overlap applied to the default chunker.</param>
    /// <returns>The built service provider hosting the full RAG graph.</returns>
    private static ServiceProvider CreateRagServiceProvider(
        DeterministicFakeEmbeddingProvider embeddingProvider,
        int maxChunkLength = 4,
        int chunkOverlap = 0)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IRagEmbeddingProvider>(embeddingProvider);
        services.AddRuniqRag(builder => builder.UseInMemoryVectorStore());
        services.Configure<RagOptions>(options =>
        {
            options.Chunking.MaxChunkLength = maxChunkLength;
            options.Chunking.ChunkOverlap = chunkOverlap;
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a vector index with the deterministic embedding dimensions so subsequent upserts pass
    /// the dimension validation applied by the registered vector store decorator.
    /// </summary>
    /// <param name="vectorStore">The vector store resolved from the dependency injection graph.</param>
    /// <param name="indexName">The name of the vector index to create.</param>
    private static async Task CreateIndexAsync(IRagVectorStore vectorStore, string indexName)
    {
        var result = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = indexName,
            Dimensions = EmbeddingDimensions,
        });

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Runs the document through the ingestion service resolved from the dependency injection graph.
    /// </summary>
    /// <param name="serviceProvider">The service provider hosting the RAG graph.</param>
    /// <param name="document">The document to ingest.</param>
    /// <returns>The ingestion result containing chunks paired with their embeddings.</returns>
    private static async Task<RagDocumentIngestionResult> IngestAsync(
        IServiceProvider serviceProvider,
        RagDocument document)
    {
        using var scope = serviceProvider.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<IRagDocumentIngestionService>();

        return await ingestionService.IngestAsync(document);
    }

    /// <summary>
    /// Writes the ingestion output through the upsert pipeline resolved from the dependency injection graph.
    /// </summary>
    /// <param name="serviceProvider">The service provider hosting the RAG graph.</param>
    /// <param name="ingestionResult">The ingestion output to write.</param>
    /// <param name="indexName">The target vector index name.</param>
    /// <param name="documentMetadata">The document metadata carried into the vector record metadata.</param>
    /// <param name="expectedDimensions">Optional expected dimensions used to trigger pipeline-level dimension validation.</param>
    /// <returns>The normalized upsert result produced by the pipeline.</returns>
    private static async Task<UpsertVectorResult> UpsertAsync(
        IServiceProvider serviceProvider,
        RagDocumentIngestionResult ingestionResult,
        string indexName,
        RagDocumentMetadata documentMetadata,
        int? expectedDimensions = null)
    {
        using var scope = serviceProvider.CreateScope();
        var upsertPipeline = scope.ServiceProvider.GetRequiredService<IRagVectorStoreUpsertPipeline>();

        return await upsertPipeline.UpsertAsync(ingestionResult, indexName, documentMetadata, expectedDimensions);
    }

    /// <summary>
    /// Reads every record stored under the specified index by issuing a zero-vector query, which scores
    /// all records equally and therefore returns them deterministically ordered by record id.
    /// </summary>
    /// <param name="vectorStore">The vector store resolved from the dependency injection graph.</param>
    /// <param name="indexName">The vector index to read.</param>
    /// <returns>The stored vector search results including metadata and vector values.</returns>
    private static async Task<IReadOnlyList<VectorSearchResult>> QueryAllRecordsAsync(
        IRagVectorStore vectorStore,
        string indexName)
    {
        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = indexName,
            Values = new float[EmbeddingDimensions],
            TopK = 100,
            IncludeMetadata = true,
            IncludeVectors = true,
        });

        Assert.True(result.Succeeded);

        return result.Records.ToList();
    }

    /// <summary>
    /// Creates a document with stable identity, content, and source metadata for the integration flow tests.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="content">The document content that will be chunked.</param>
    /// <param name="additionalMetadata">Optional additional key/value document metadata.</param>
    /// <returns>The document instance used as ingestion input.</returns>
    private static RagDocument CreateDocument(
        string id,
        string content,
        RagMetadata? additionalMetadata = null)
    {
        return new RagDocument
        {
            Id = id,
            Content = content,
            Metadata = new RagDocumentMetadata
            {
                SourceId = "source-1",
                SourceName = "Product handbook",
                SourceUri = "https://example.test/product-handbook",
                ContentType = "text/plain",
                AdditionalMetadata = additionalMetadata ?? new RagMetadata(),
            },
        };
    }

    /// <summary>
    /// Provides deterministic embeddings for integration tests without calling an external provider,
    /// and records every text it embeds so tests can prove this provider produced the stored vectors.
    /// </summary>
    private sealed class DeterministicFakeEmbeddingProvider : IRagEmbeddingProvider
    {
        /// <summary>
        /// Gets the texts that were sent to the provider, in the order they were embedded.
        /// </summary>
        public IList<string> Texts { get; } = new List<string>();

        /// <summary>
        /// Generates a deterministic embedding derived only from the supplied text, without any
        /// network, SDK, or external provider call.
        /// </summary>
        /// <param name="text">The chunk content to embed.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
        /// <returns>The deterministic embedding for the text.</returns>
        public Task<RagEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Texts.Add(text);

            return Task.FromResult(new RagEmbedding(CreateEmbeddingValues(text)));
        }

        /// <summary>
        /// Computes the deterministic embedding values for the supplied text. The vector is
        /// content-sensitive (length, character checksum, first character) yet stable across runs,
        /// so assertions can recompute the expected vector from stored record content.
        /// </summary>
        /// <param name="text">The text to convert into deterministic vector values.</param>
        /// <returns>The deterministic vector values with <see cref="EmbeddingDimensions"/> dimensions.</returns>
        public static IReadOnlyList<float> CreateEmbeddingValues(string text)
        {
            var checksum = text.Sum(character => character);

            return [text.Length, checksum, text.Length == 0 ? 0 : text[0]];
        }
    }
}

