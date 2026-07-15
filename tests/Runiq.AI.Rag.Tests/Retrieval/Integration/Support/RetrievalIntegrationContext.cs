using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Tools;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Tools;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.Tests.Retrieval.Integration.Support;

/// <summary>
/// A reusable end-to-end harness for retrieval integration tests. It builds the real RAG dependency injection
/// graph through <c>AddRuniqRag</c> with the in-memory vector store and the deterministic keyword embedding,
/// so tests exercise the production wiring (query text → embedding → vector search → filtered TopK result)
/// without any real embedding provider, network, or database. It also owns the small setup chores of creating
/// indexes and seeding records so the tests stay focused on retrieval assertions.
/// </summary>
public sealed class RetrievalIntegrationContext : IDisposable
{
    private readonly ServiceProvider serviceProvider;
    private readonly DeterministicKeywordEmbeddingProvider embeddingClient;
    private readonly IRagVectorStore vectorStore;
    private readonly IRagRetrievalPipeline retrievalPipeline;
    private readonly IVectorQueryTool vectorQueryTool;
    private readonly HashSet<string> createdIndexes = new(StringComparer.Ordinal);

    private RetrievalIntegrationContext(
        ServiceProvider serviceProvider,
        DeterministicKeywordEmbeddingProvider embeddingClient)
    {
        this.serviceProvider = serviceProvider;
        this.embeddingClient = embeddingClient;
        vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        retrievalPipeline = serviceProvider.GetRequiredService<IRagRetrievalPipeline>();
        vectorQueryTool = serviceProvider.GetRequiredService<IVectorQueryTool>();
    }

    /// <summary>
    /// Builds a retrieval integration context wired with the deterministic embedding and the in-memory vector
    /// store, resolving the retrieval pipeline from the same container the production code uses.
    /// </summary>
    /// <returns>The initialized retrieval integration context.</returns>
    public static RetrievalIntegrationContext Create()
    {
        var embeddingClient = new DeterministicKeywordEmbeddingProvider();
        var services = new ServiceCollection();

        services.AddSingleton<IEmbeddingClient>(embeddingClient);
        services.AddRuniqRag(builder => builder.UseInMemoryVectorStore());

        return new RetrievalIntegrationContext(services.BuildServiceProvider(), embeddingClient);
    }

    /// <summary>
    /// Ensures the named index exists with the deterministic embedding dimensions but seeds no records, so tests
    /// can exercise the empty-index / no-result path.
    /// </summary>
    /// <param name="indexName">The index to create.</param>
    public async Task CreateEmptyIndexAsync(string indexName)
    {
        await EnsureIndexAsync(indexName);
    }

    /// <summary>
    /// Creates the named index if needed and writes the supplied records into it, embedding each record's
    /// content through the same deterministic embedding the retrieval pipeline uses for query text.
    /// </summary>
    /// <param name="indexName">The target vector index name.</param>
    /// <param name="records">The records to seed into the index.</param>
    public async Task SeedAsync(string indexName, params RetrievalSeedRecord[] records)
    {
        ArgumentNullException.ThrowIfNull(records);

        await EnsureIndexAsync(indexName);

        var vectorRecords = records
            .Select(record => new VectorRecord
            {
                Id = record.Id,
                Values = embeddingClient.Embed(record.Content),
                Content = record.Content,
                Metadata = new RagMetadata(record.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value)),
            })
            .ToList();

        var upsertResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = indexName,
            Records = vectorRecords,
        });

        Assert.True(upsertResult.Succeeded);
    }

    /// <summary>
    /// Runs a query-time retrieval through the resolved pipeline for the given query text and target index,
    /// optionally limiting the result count and applying a metadata filter.
    /// </summary>
    /// <param name="indexName">The index the retrieval targets.</param>
    /// <param name="queryText">The natural-language query text to embed and search with.</param>
    /// <param name="topK">The maximum number of matches to return.</param>
    /// <param name="metadataFilter">The metadata filter to apply; defaults to no filtering.</param>
    /// <returns>The retrieval result produced by the pipeline.</returns>
    public Task<RetrievalResult> RetrieveAsync(
        string indexName,
        string queryText,
        int topK = 5,
        RetrievalMetadataFilter? metadataFilter = null)
    {
        return retrievalPipeline.RetrieveAsync(new RetrievalRequest
        {
            IndexName = indexName,
            QueryText = queryText,
            TopK = topK,
            MetadataFilter = metadataFilter ?? RetrievalMetadataFilter.Empty,
        });
    }

    /// <summary>
    /// Runs the resolved <see cref="IVectorQueryTool"/> end to end against the seeded in-memory store, exercising
    /// the same production wiring the pipeline uses (query text → embedding → vector search → filtered TopK)
    /// through the agent-facing tool contract. The <paramref name="vectorStoreName"/> is an association value the
    /// tool carries but does not route on; any non-empty value drives the configured in-memory store.
    /// </summary>
    /// <param name="indexName">The index the tool query targets.</param>
    /// <param name="queryText">The natural-language query text to embed and search with.</param>
    /// <param name="vectorStoreName">The associated vector store name carried on the tool request.</param>
    /// <param name="topK">The maximum number of matches to return.</param>
    /// <param name="metadataFilter">The metadata filter to apply; defaults to no filtering.</param>
    /// <returns>The tool result produced by the resolved Vector Query Tool.</returns>
    public Task<VectorQueryToolResult> ExecuteVectorQueryToolAsync(
        string indexName,
        string queryText,
        string vectorStoreName = "in-memory-store",
        int topK = 5,
        RetrievalMetadataFilter? metadataFilter = null)
    {
        return vectorQueryTool.ExecuteAsync(new VectorQueryToolRequest
        {
            VectorStoreName = vectorStoreName,
            IndexName = indexName,
            QueryText = queryText,
            TopK = topK,
            MetadataFilter = metadataFilter ?? RetrievalMetadataFilter.Empty,
        });
    }

    /// <summary>
    /// Builds an exact-match metadata filter from a single key/value pair for the metadata-filter scenarios.
    /// </summary>
    /// <param name="key">The metadata key to constrain.</param>
    /// <param name="value">The exact value the key must equal.</param>
    /// <returns>The metadata filter carrying the single equality criterion.</returns>
    public static RetrievalMetadataFilter MetadataEquals(string key, string value)
    {
        return new RetrievalMetadataFilter(new[]
        {
            new RetrievalMetadataFilterCriterion(key, value),
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        serviceProvider.Dispose();
    }

    private async Task EnsureIndexAsync(string indexName)
    {
        if (!createdIndexes.Add(indexName))
        {
            return;
        }

        var result = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = indexName,
            Dimensions = embeddingClient.Dimensions,
        });

        Assert.True(result.Succeeded);
    }
}

