using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;
using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Ingestion;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Runtime;
using Runiq.AI.Rag.VectorStores.InMemory;

namespace Runiq.AI.Rag.Tests.Runtime;

public sealed class RagIndexRuntimeConfigurationTests
{
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
}
