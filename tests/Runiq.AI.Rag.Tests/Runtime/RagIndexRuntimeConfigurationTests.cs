using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;
using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.Runtime;
using Runiq.AI.Rag.VectorStores.InMemory;

namespace Runiq.AI.Rag.Tests.Runtime;

public sealed class RagIndexRuntimeConfigurationTests
{
    [Fact]
    // Verifies lexical-only retrieval resolves the named store without requiring or falling back through embedding configuration.
    public async Task LexicalRetrieval_ShouldUseNamedStoreWithoutEmbeddingClient()
    {
        var services = new ServiceCollection();
        var globalStore = new InMemoryRagVectorStore();
        var namedStore = new InMemoryRagVectorStore();
        services.AddRuniqRag(rag => rag.AddIndex("documents",
            index => index.AddSource(new TextSource("source")).UseEmbeddingModel("openai/not-registered").UseVectorStore("named")));
        services.AddRagVectorStore(_ => globalStore);
        services.AddRagVectorStore("named", _ => namedStore);
        await namedStore.CreateIndexAsync(new() { IndexName = "documents", Dimensions = 2 });
        await namedStore.UpsertAsync(new()
        {
            IndexName = "documents",
            Records = [new() { Id = "named", Content = "IRagRetriever named store", Values = [1f, 0f] }],
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IRagRetrievalPipeline>().RetrieveAsync(new()
        {
            IndexName = "documents",
            QueryText = "IRagRetriever",
            Mode = RagRetrievalMode.Lexical,
        });

        Assert.True(result.Succeeded);
        Assert.Equal("named", Assert.Single(result.Items).RecordId);
    }

    [Fact]
    // Verifies a named store without lexical capability fails explicitly instead of using the global store.
    public async Task LexicalRetrieval_ShouldFailWhenNamedStoreHasNoLexicalCapability()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents",
            index => index.AddSource(new TextSource("source")).UseEmbeddingModel("openai/not-registered").UseVectorStore("semantic-only")));
        services.AddRagVectorStore(_ => new InMemoryRagVectorStore());
        services.AddRagVectorStore("semantic-only", _ => new SemanticOnlyStore());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IRagRetrievalPipeline>().RetrieveAsync(new()
        {
            IndexName = "documents",
            QueryText = "query",
            Mode = RagRetrievalMode.Lexical,
        });

        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.VectorStoreQueryFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(RagRetrievalMode.Semantic)]
    [InlineData(RagRetrievalMode.Hybrid)]
    // Verifies named semantic and hybrid modes still require their configured embedding dependency.
    public async Task SemanticModes_ShouldRejectMissingNamedEmbedding(RagRetrievalMode mode)
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents",
            index => index.AddSource(new TextSource("source")).UseEmbeddingModel("openai/missing").UseVectorStore("semantic-only")));
        services.AddRagVectorStore("semantic-only", _ => new SemanticOnlyStore());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IRagRetrievalPipeline>().RetrieveAsync(new()
            {
                IndexName = "documents",
                QueryText = "query",
                Mode = mode,
            }));

        Assert.Contains("unregistered embedding", exception.Message, StringComparison.Ordinal);
    }

    // Verifies two indexes concurrently use isolated chunking, embedding models, clients, and vector stores through the production pipeline.
    [Fact]
    public async Task DifferentIndexes_ShouldUseIsolatedEffectiveRuntimeConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var clientA = new RecordingEmbeddingClient();
        var clientB = new RecordingEmbeddingClient();
        var storeA = new InMemoryRagVectorStore();
        var storeB = new InMemoryRagVectorStore();
        services.AddRuniqRag(rag =>
        {
            rag.AddIndex("index-a", index => index.AddSource(new TextSource("a")).UseEmbeddingModel("openai/model-a").UseVectorStore("store-a").ConfigureChunking(8, 0));
            rag.AddIndex("index-b", index => index.AddSource(new TextSource("b")).UseEmbeddingModel("openai/model-b").UseVectorStore("store-b").ConfigureChunking(32, 0));
        });
        services.AddRagEmbeddingClient("openai/model-a", _ => clientA);
        services.AddRagEmbeddingClient("openai/model-b", _ => clientB);
        services.AddRagVectorStore("store-a", _ => storeA);
        services.AddRagVectorStore("store-b", _ => storeB);
        services.Configure<RagOptions>(options =>
        {
            options.EmbeddingModel = "openai/global-model";
            options.Chunking.MaxChunkLength = 999;
            options.Chunking.ChunkOverlap = 99;
        });
        await using var provider = services.BuildServiceProvider();

        var manager = provider.GetRequiredService<IRagIngestionManager>();
        await Task.WhenAll(manager.StartAsync("index-a"), manager.StartAsync("index-b"));

        Assert.Equal("model-a", clientA.Models.Single());
        Assert.Equal("model-b", clientB.Models.Single());
        Assert.True(clientA.InputCounts.Single() > clientB.InputCounts.Single());
        using var scope = provider.CreateScope();
        var retrieval = scope.ServiceProvider.GetRequiredService<IRagRetrievalPipeline>();
        Assert.True((await retrieval.RetrieveAsync(new RetrievalRequest { IndexName = "index-a", QueryText = "policy", TopK = 3 })).Succeeded);
        Assert.True((await retrieval.RetrieveAsync(new RetrievalRequest { IndexName = "index-b", QueryText = "policy", TopK = 3 })).Succeeded);
        Assert.Equal(2, clientA.Models.Count);
        Assert.Equal(2, clientB.Models.Count);
        var registrations = provider.GetRequiredService<IRagIndexRegistry>().Registrations.ToDictionary(item => item.Name);
        Assert.Equal(8, registrations["index-a"].Chunking.MaxChunkLength);
        Assert.Equal(32, registrations["index-b"].Chunking.MaxChunkLength);
        var runtimeA = scope.ServiceProvider.GetRequiredService<IRagIndexRuntimeConfigurationResolver>().Resolve("index-a");
        Assert.Same(storeA, runtimeA.VectorStore);
        Assert.Equal("model-a", runtimeA.EmbeddingModel.ModelName);
        Assert.Equal(8, runtimeA.Chunking.MaxChunkLength);
        var global = provider.GetRequiredService<IOptions<RagOptions>>().Value;
        Assert.Equal("openai/global-model", global.EmbeddingModel);
        Assert.Equal(999, global.Chunking.MaxChunkLength);
    }

    // Verifies an explicit index embedding reference fails clearly when no matching runtime client is registered.
    [Fact]
    public void Resolver_ShouldRejectUnknownEmbeddingReference()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index => index.AddSource(new TextSource("source")).UseEmbeddingModel("openai/missing").UseInMemoryVectorStore()));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IRagIndexRuntimeConfigurationResolver>().Resolve("documents"));

        Assert.Contains("unregistered embedding", exception.Message, StringComparison.Ordinal);
    }

    // Verifies an explicit index store reference fails clearly instead of falling back to the global store.
    [Fact]
    public void Resolver_ShouldRejectUnknownVectorStoreReference()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index => index.AddSource(new TextSource("source")).UseEmbeddingModel("openai/model").UseVectorStore("missing-store")));
        services.AddRagEmbeddingClient("openai/model", _ => new RecordingEmbeddingClient());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IRagIndexRuntimeConfigurationResolver>().Resolve("documents"));

        Assert.Contains("unregistered vector store", exception.Message, StringComparison.Ordinal);
    }

    // Verifies legacy unregistered-index calls retain explicitly configured global client, model, store, and chunking defaults.
    [Fact]
    public void Resolver_ShouldUseGlobalDefaults_WhenNoIndexRegistrationExists()
    {
        var services = new ServiceCollection();
        var client = new RecordingEmbeddingClient();
        var store = new InMemoryRagVectorStore();
        services.AddRuniqRag();
        services.AddRagEmbeddingClient(_ => client);
        services.AddRagVectorStore(_ => store);
        services.Configure<RagOptions>(options =>
        {
            options.EmbeddingModel = "openai/global-model";
            options.Chunking.MaxChunkLength = 77;
            options.Chunking.ChunkOverlap = 7;
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var runtime = scope.ServiceProvider.GetRequiredService<IRagIndexRuntimeConfigurationResolver>().Resolve("legacy-index");

        Assert.Same(client, runtime.EmbeddingClient);
        Assert.Same(provider.GetRequiredService<IRagVectorStore>(), runtime.VectorStore);
        Assert.Equal("global-model", runtime.EmbeddingModel.ModelName);
        Assert.Equal(77, runtime.Chunking.MaxChunkLength);
    }

    private sealed class TextSource(string identity) : IRagDocumentSource
    {
        public string Identity => identity;
        public Task<IReadOnlyList<RagSourceDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RagSourceDocument>>([new() { Id = "policy", Content = "Corporate policy content with enough text for several small chunks.", ContentType = "text/plain" }]);
    }

    private sealed class RecordingEmbeddingClient : IEmbeddingClient
    {
        public List<string> Models { get; } = [];
        public List<int> InputCounts { get; } = [];
        public Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            Models.Add(request.Model.ModelName);
            InputCounts.Add(request.Inputs.Count);
            return Task.FromResult(new EmbeddingResponse(request.Inputs.Select((_, index) => new EmbeddingResult(index, [1f, 0f, 0f], 3)).ToArray()));
        }
    }

    private sealed class SemanticOnlyStore : IRagVectorStore
    {
        public Task<CreateVectorIndexResult> CreateIndexAsync(
            CreateVectorIndexRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Runiq.AI.Rag.Models.Search.RagSearchResult>> SearchAsync(
            Runiq.AI.Rag.Models.Queries.RagQuery query,
            Runiq.AI.Rag.Models.Embeddings.RagEmbedding embedding,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<QueryVectorResult> QueryAsync(
            QueryVectorRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryVectorResult { Succeeded = true, Records = [] });
    }
}
