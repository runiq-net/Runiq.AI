using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.CorporateDocumentAssistant.Models;
using Runiq.AI.Rag.CorporateDocumentAssistant.Services;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.Tests.Samples;

/// <summary>
/// Covers the provider-independent ingestion behavior of the Corporate Document Assistant sample.
/// </summary>
public sealed class CorporateDocumentAssistantSampleTests
{
    // Verifies that a submitted plain-text document is chunked, embedded, and upserted into the configured sample index.
    [Fact]
    public async Task IngestAsync_ShouldChunkEmbedAndUpsertDocument()
    {
        using var serviceProvider = CreateServiceProvider("corporate-documents-test");
        using var scope = serviceProvider.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<CorporateDocumentIngestionService>();

        var response = await ingestionService.IngestAsync(new CorporateDocumentIngestionRequest
        {
            Id = "vpn-guide",
            Title = "VPN Guide",
            Content = string.Join(
                Environment.NewLine,
                Enumerable.Repeat("VPN users should restart the client and verify multi-factor authentication before contacting IT support.", 12)),
        });

        Assert.True(response.UpsertSucceeded);
        Assert.Equal("corporate-documents-test", response.IndexName);
        Assert.True(response.ChunkCount > 1);
        Assert.Equal(response.ChunkCount, response.EmbeddingCount);
        Assert.Equal(response.ChunkCount, response.UpsertedCount);
        Assert.Equal(response.ChunkCount, response.VectorIds.Count);
    }

    // Verifies that vector records written by the sample retain document and chunk metadata needed by later source display work.
    [Fact]
    public async Task IngestAsync_ShouldRetainSourceMetadataInVectorRecords()
    {
        using var serviceProvider = CreateServiceProvider("corporate-source-metadata-test");
        using var scope = serviceProvider.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<CorporateDocumentIngestionService>();
        var vectorStore = scope.ServiceProvider.GetRequiredService<IRagVectorStore>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<CorporateDocumentAssistantOptions>>().Value;

        await ingestionService.IngestAsync(new CorporateDocumentIngestionRequest
        {
            Id = "password-policy",
            Title = "Password Policy",
            Content = "Corporate passwords must be unique, protected with multi-factor authentication, and changed after suspected exposure.",
        });

        var queryResult = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = options.IndexName,
            Values = DeterministicCorporateEmbeddingProvider.CreateEmbeddingValues("password multi-factor authentication"),
            TopK = 1,
            IncludeMetadata = true,
        });

        var record = Assert.Single(queryResult.Records);
        Assert.True(queryResult.Succeeded);
        Assert.Equal("password-policy", record.Metadata.Values["sourceId"]);
        Assert.Equal("Password Policy", record.Metadata.Values["sourceName"]);
        Assert.Equal("password-policy", record.Metadata.Values["documentId"]);
        Assert.True(record.Metadata.Values.ContainsKey("chunkId"));
        Assert.True(record.Metadata.Values.ContainsKey("chunkIndex"));
    }

    // Verifies that the sample embedding provider is deterministic and advertises the dimensions used for index creation.
    [Fact]
    public async Task DeterministicCorporateEmbeddingProvider_ShouldReturnStableVectorsWithAdvertisedDimensions()
    {
        var provider = new DeterministicCorporateEmbeddingProvider();

        var first = await provider.GenerateAsync("same corporate document chunk");
        var second = await provider.GenerateAsync("same corporate document chunk");

        Assert.Equal(DeterministicCorporateEmbeddingProvider.Dimensions, first.Dimensions);
        Assert.Equal(first.Values, second.Values);
    }

    // Verifies that the sample query-time flow retrieves stored chunks and exposes them as answer sources.
    [Fact]
    public async Task AskAsync_ShouldReturnAnswerWithRetrievedSources()
    {
        using var serviceProvider = CreateServiceProvider("corporate-query-test");
        using var scope = serviceProvider.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<CorporateDocumentIngestionService>();
        var queryService = scope.ServiceProvider.GetRequiredService<CorporateDocumentQueryService>();

        await ingestionService.IngestAsync(new CorporateDocumentIngestionRequest
        {
            Id = "vpn-baglanti-rehberi",
            Title = "vpn-baglanti-rehberi.md",
            Content = "VPN baglantisi calismiyorsa kullanici internet baglantisini kontrol eder, VPN istemcisini yeniden baslatir ve MFA bildirimini dogrular.",
        });

        var response = await queryService.AskAsync(new CorporateDocumentQueryRequest
        {
            Question = "VPN baglantisi calismiyorsa ne yapmaliyim?",
            TopK = 2,
        });

        Assert.Equal("corporate-query-test", response.IndexName);
        Assert.Contains("Demo answer", response.Answer, StringComparison.Ordinal);
        var source = Assert.Single(response.Sources);
        Assert.Equal("vpn-baglanti-rehberi", source.DocumentId);
        Assert.Equal("vpn-baglanti-rehberi.md", source.SourceName);
        Assert.Contains("VPN", source.Snippet, StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceProvider CreateServiceProvider(string indexName)
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(builder => builder.UseInMemoryVectorStore());
        services.AddRagEmbeddingProvider<DeterministicCorporateEmbeddingProvider>();
        services.Configure<RagOptions>(options =>
        {
            options.Chunking.MaxChunkLength = 180;
            options.Chunking.ChunkOverlap = 20;
        });
        services.Configure<CorporateDocumentAssistantOptions>(options => options.IndexName = indexName);
        services.AddScoped<CorporateDocumentIngestionService>();
        services.AddScoped<CorporateDocumentQueryService>();

        return services.BuildServiceProvider();
    }
}

