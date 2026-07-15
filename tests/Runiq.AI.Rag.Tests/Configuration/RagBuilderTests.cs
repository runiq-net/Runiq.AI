using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Rag.Abstractions.Chunking;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Tools;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.Tools;
using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.VectorStores.InMemory;

namespace Runiq.AI.Rag.Tests.Configuration;

public sealed class RagBuilderTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenServicesIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new RagBuilder(null!));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void UseEmbeddingClient_ShouldReplaceDefaultCoreEmbeddingClientRegistration()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();
        var builder = new RagBuilder(services);

        builder.UseEmbeddingClient<TestEmbeddingClient>();

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IEmbeddingClient));
        Assert.Equal(typeof(TestEmbeddingClient), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        using var serviceProvider = services.BuildServiceProvider();
        Assert.IsType<TestEmbeddingClient>(serviceProvider.GetRequiredService<IEmbeddingClient>());
    }

    [Fact]
    public void UseVectorStore_ShouldReplaceDefaultVectorStoreRegistration()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();
        var builder = new RagBuilder(services);

        builder.UseVectorStore<TestVectorStore>();

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IRagVectorStore));
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void UseInMemoryVectorStore_ShouldReplaceDefaultVectorStoreRegistration()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();
        var builder = new RagBuilder(services);

        builder.UseInMemoryVectorStore();

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IRagVectorStore));
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void UseVectorStoreWithFactory_ShouldReplaceDefaultVectorStoreRegistration()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();
        var builder = new RagBuilder(services);

        builder.UseVectorStore(_ => new TestVectorStore());

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IRagVectorStore));
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void UseVectorStoreWithFactory_ShouldThrow_WhenFactoryIsNull()
    {
        var builder = new RagBuilder(new ServiceCollection());

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.UseVectorStore((Func<IServiceProvider, IRagVectorStore>)null!));

        Assert.Equal("factory", exception.ParamName);
    }

    [Fact]
    public void UseRetriever_ShouldReplaceDefaultRetrieverRegistration()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();
        var builder = new RagBuilder(services);

        builder.UseRetriever<TestRetriever>();

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IRagRetriever));
        Assert.Equal(typeof(TestRetriever), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void UseChunker_ShouldReplaceDefaultChunkerRegistration()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();
        var builder = new RagBuilder(services);

        builder.UseChunker<TestChunker>();

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IRagChunker));
        Assert.Equal(typeof(TestChunker), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void UseVectorQueryTool_ShouldReplaceDefaultVectorQueryToolRegistration()
    {
        // Verifies that the builder replaces the default Vector Query Tool with a single scoped registration, matching the retrieval pipeline lifetime it delegates to.
        var services = new ServiceCollection();
        services.AddRuniqRag();
        var builder = new RagBuilder(services);

        builder.UseVectorQueryTool<TestVectorQueryTool>();

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IVectorQueryTool));
        Assert.Equal(typeof(TestVectorQueryTool), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void UseEmbeddingClient_ShouldReturnSameBuilderInstance()
    {
        var builder = new RagBuilder(new ServiceCollection());

        var returnedBuilder = builder.UseEmbeddingClient<TestEmbeddingClient>();

        Assert.Same(builder, returnedBuilder);
    }

    [Fact]
    public void UseVectorStore_ShouldReturnSameBuilderInstance()
    {
        var builder = new RagBuilder(new ServiceCollection());

        var returnedBuilder = builder.UseVectorStore<TestVectorStore>();

        Assert.Same(builder, returnedBuilder);
    }

    [Fact]
    public void UseInMemoryVectorStore_ShouldReturnSameBuilderInstance()
    {
        var builder = new RagBuilder(new ServiceCollection());

        var returnedBuilder = builder.UseInMemoryVectorStore();

        Assert.Same(builder, returnedBuilder);
    }

    [Fact]
    public void UseVectorStoreWithFactory_ShouldReturnSameBuilderInstance()
    {
        var builder = new RagBuilder(new ServiceCollection());

        var returnedBuilder = builder.UseVectorStore(_ => new TestVectorStore());

        Assert.Same(builder, returnedBuilder);
    }

    [Fact]
    public void UseRetriever_ShouldReturnSameBuilderInstance()
    {
        var builder = new RagBuilder(new ServiceCollection());

        var returnedBuilder = builder.UseRetriever<TestRetriever>();

        Assert.Same(builder, returnedBuilder);
    }

    [Fact]
    public void UseChunker_ShouldReturnSameBuilderInstance()
    {
        var builder = new RagBuilder(new ServiceCollection());

        var returnedBuilder = builder.UseChunker<TestChunker>();

        Assert.Same(builder, returnedBuilder);
    }

    [Fact]
    public void UseVectorQueryTool_ShouldReturnSameBuilderInstance()
    {
        // Verifies that the Vector Query Tool override returns the same builder so calls can be chained.
        var builder = new RagBuilder(new ServiceCollection());

        var returnedBuilder = builder.UseVectorQueryTool<TestVectorQueryTool>();

        Assert.Same(builder, returnedBuilder);
    }

    private sealed class TestEmbeddingClient : IEmbeddingClient
    {
        public Task<EmbeddingResponse> EmbedAsync(
            EmbeddingRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var results = request.Inputs.Select((input, index) =>
            {
                IReadOnlyList<float> vector = [input.Length];
                return new EmbeddingResult(index, vector, vector.Count);
            }).ToList();

            return Task.FromResult(new EmbeddingResponse(results));
        }
    }

    private sealed class TestVectorStore : IRagVectorStore
    {
        public Task<CreateVectorIndexResult> CreateIndexAsync(
            CreateVectorIndexRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CreateVectorIndexResult
            {
                IndexName = request.IndexName,
                Succeeded = true,
            });
        }

        public Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UpsertVectorResult
            {
                Succeeded = true,
                UpsertedCount = request.Records?.Count ?? 0,
            });
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RagSearchResult>>(Array.Empty<RagSearchResult>());
        }
    }

    private sealed class TestRetriever : IRagRetriever
    {
        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RagSearchResult>>(Array.Empty<RagSearchResult>());
        }
    }

    private sealed class TestChunker : IRagChunker
    {
        public Task<IReadOnlyList<RagChunk>> ChunkAsync(
            RagDocument document,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RagChunk>>(Array.Empty<RagChunk>());
        }
    }

    private sealed class TestVectorQueryTool : IVectorQueryTool
    {
        public Task<VectorQueryToolResult> ExecuteAsync(
            VectorQueryToolRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(VectorQueryToolResult.Success());
        }
    }
}

