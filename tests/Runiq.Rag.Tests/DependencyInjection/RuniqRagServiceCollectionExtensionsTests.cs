using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runiq.Rag.Abstractions.Chunking;
using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Abstractions.Retrieval;
using Runiq.Rag.Abstractions.Services;
using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Chunking;
using Runiq.Rag.Configuration;
using Runiq.Rag.DependencyInjection;
using Runiq.Rag.Embeddings;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;
using Runiq.Rag.Models.VectorStores;
using Runiq.Rag.VectorStores;
using Runiq.Rag.VectorStores.InMemory;

namespace Runiq.Rag.Tests.DependencyInjection;

public sealed class RuniqRagServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRuniqRag_ShouldThrow_WhenServicesIsNull()
    {
        IServiceCollection services = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => services.AddRuniqRag());

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagService()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagService));
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagRetriever()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagRetriever));
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagEmbeddingProvider()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagEmbeddingProvider));
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagEmbeddingInputPreparer()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagEmbeddingInputPreparer));
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagVectorStore()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagVectorStore));
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagChunker()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagChunker));
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveRagService()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IRagService>());
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveRagRetriever()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IRagRetriever>());
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveRagChunker()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<DefaultRagChunker>(serviceProvider.GetRequiredService<IRagChunker>());
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveNullEmbeddingProviderByDefault()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<NullEmbeddingProvider>(serviceProvider.GetRequiredService<IRagEmbeddingProvider>());
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveDefaultEmbeddingInputPreparer()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<DefaultRagEmbeddingInputPreparer>(
            serviceProvider.GetRequiredService<IRagEmbeddingInputPreparer>());
    }

    [Fact]
    public void AddRuniqRag_ShouldNotOverwriteUserRegisteredEmbeddingInputPreparer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRagEmbeddingInputPreparer, TestEmbeddingInputPreparer>();

        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestEmbeddingInputPreparer>(serviceProvider.GetRequiredService<IRagEmbeddingInputPreparer>());
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveNullVectorStoreByDefault()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<NullVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddRuniqRag_ShouldNotOverwriteUserRegisteredEmbeddingProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRagEmbeddingProvider, TestEmbeddingProvider>();

        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestEmbeddingProvider>(serviceProvider.GetRequiredService<IRagEmbeddingProvider>());
    }

    [Fact]
    public void AddRuniqRag_ShouldNotOverwriteUserRegisteredVectorStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRagVectorStore, TestVectorStore>();

        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddInMemoryRagVectorStore_ShouldRegisterInMemoryVectorStore()
    {
        var services = new ServiceCollection();

        services.AddInMemoryRagVectorStore();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<InMemoryRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddInMemoryRagVectorStore_ShouldOverrideDefaultVectorStore_WhenRegisteredAfterAddRuniqRag()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();
        services.AddInMemoryRagVectorStore();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<InMemoryRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddInMemoryRagVectorStore_ShouldNotRequireProviderConfiguration()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(rag => rag.UseInMemoryVectorStore());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<InMemoryRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagService>());
    }

    [Fact]
    public void AddRagVectorStore_ShouldRegisterCustomVectorStore()
    {
        var services = new ServiceCollection();

        services.AddRagVectorStore<TestVectorStore>();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddRagVectorStoreWithFactory_ShouldRegisterCustomVectorStoreInstance()
    {
        var services = new ServiceCollection();
        var vectorStore = new TestVectorStore();

        services.AddRagVectorStore(_ => vectorStore);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Same(vectorStore, serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddRagVectorStore_ShouldAllowTestVectorStoreToBeChanged()
    {
        var services = new ServiceCollection();

        services.AddRagVectorStore<TestVectorStore>();
        services.AddInMemoryRagVectorStore();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<InMemoryRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddRuniqRagWithConfigure_ShouldThrow_WhenServicesIsNull()
    {
        IServiceCollection services = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => services.AddRuniqRag(_ => { }));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void AddRuniqRagWithConfigure_ShouldThrow_WhenConfigureIsNull()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            services.AddRuniqRag((Action<RagBuilder>)null!));

        Assert.Equal("configure", exception.ParamName);
    }

    [Fact]
    public void AddRuniqRagWithConfigure_ShouldRegisterDefaultServices_WhenNoOverrideIsUsed()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(_ => { });

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<NullEmbeddingProvider>(serviceProvider.GetRequiredService<IRagEmbeddingProvider>());
        Assert.IsType<NullVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagRetriever>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagChunker>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagEmbeddingInputPreparer>());
    }

    [Fact]
    public void AddRuniqRagWithConfigure_ShouldAllowCustomEmbeddingProvider()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(rag => rag.UseEmbedding<TestEmbeddingProvider>());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestEmbeddingProvider>(serviceProvider.GetRequiredService<IRagEmbeddingProvider>());
    }

    [Fact]
    public void AddRuniqRagWithConfigure_ShouldAllowCustomVectorStore()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(rag => rag.UseVectorStore<TestVectorStore>());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddRuniqRagWithConfigure_ShouldAllowInMemoryVectorStore()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(rag => rag.UseInMemoryVectorStore());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<InMemoryRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddRuniqRagWithConfigure_ShouldAllowCustomVectorStoreFactory()
    {
        var services = new ServiceCollection();
        var vectorStore = new TestVectorStore();

        services.AddRuniqRag(rag => rag.UseVectorStore(_ => vectorStore));

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Same(vectorStore, serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddRuniqRagWithConfigure_ShouldAllowCustomRetriever()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(rag => rag.UseRetriever<TestRetriever>());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestRetriever>(serviceProvider.GetRequiredService<IRagRetriever>());
    }

    [Fact]
    public void AddRuniqRagWithConfigure_ShouldAllowCustomChunker()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(rag => rag.UseChunker<TestChunker>());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestChunker>(serviceProvider.GetRequiredService<IRagChunker>());
    }

    [Fact]
    public void AddRuniqRagWithConfigure_ShouldResolveRagService_WhenCustomProvidersAreConfigured()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(rag =>
        {
            rag.UseEmbedding<TestEmbeddingProvider>();
            rag.UseVectorStore<TestVectorStore>();
            rag.UseRetriever<TestRetriever>();
            rag.UseChunker<TestChunker>();
        });

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IRagService>());
        Assert.IsType<TestChunker>(serviceProvider.GetRequiredService<IRagChunker>());
    }

    [Fact]
    public void AddRuniqRagWithConfiguration_ShouldThrow_WhenServicesIsNull()
    {
        IServiceCollection services = null!;
        var configuration = CreateConfiguration();

        var exception = Assert.Throws<ArgumentNullException>(() => services.AddRuniqRag(configuration));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void AddRuniqRagWithConfiguration_ShouldThrow_WhenConfigurationIsNull()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentNullException>(() => services.AddRuniqRag((IConfiguration)null!));

        Assert.Equal("configuration", exception.ParamName);
    }

    [Fact]
    public void AddRuniqRagWithConfigurationAndConfigure_ShouldThrow_WhenServicesIsNull()
    {
        IServiceCollection services = null!;
        var configuration = CreateConfiguration();

        var exception = Assert.Throws<ArgumentNullException>(() => services.AddRuniqRag(configuration, _ => { }));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void AddRuniqRagWithConfigurationAndConfigure_ShouldThrow_WhenConfigurationIsNull()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            services.AddRuniqRag(null!, _ => { }));

        Assert.Equal("configuration", exception.ParamName);
    }

    [Fact]
    public void AddRuniqRagWithConfigurationAndConfigure_ShouldThrow_WhenConfigureIsNull()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            services.AddRuniqRag(configuration, null!));

        Assert.Equal("configure", exception.ParamName);
    }

    [Fact]
    public void AddRuniqRagWithConfiguration_ShouldBindRagOptions()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Runiq:Rag:DefaultTopK"] = "8",
            ["Runiq:Rag:ContextSeparator"] = "\n---\n",
            ["Runiq:Rag:EnableEmptyContext"] = "false",
            ["Runiq:Rag:Chunking:MaxChunkLength"] = "12",
            ["Runiq:Rag:Chunking:ChunkOverlap"] = "3",
        });

        services.AddRuniqRag(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<RagOptions>>().Value;

        Assert.Equal(8, options.DefaultTopK);
        Assert.Equal("\n---\n", options.ContextSeparator);
        Assert.False(options.EnableEmptyContext);
        Assert.Equal(12, options.Chunking.MaxChunkLength);
        Assert.Equal(3, options.Chunking.ChunkOverlap);
    }

    [Fact]
    public void AddRuniqRagWithConfiguration_ShouldRegisterRagService()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(CreateConfiguration());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IRagService>());
    }

    [Fact]
    public void AddRuniqRagWithConfiguration_ShouldRegisterRagRetriever()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(CreateConfiguration());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IRagRetriever>());
    }

    [Fact]
    public void AddRuniqRagWithConfiguration_ShouldRegisterRagEmbeddingProvider()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(CreateConfiguration());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IRagEmbeddingProvider>());
    }

    [Fact]
    public void AddRuniqRagWithConfiguration_ShouldRegisterRagVectorStore()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(CreateConfiguration());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddRuniqRagWithConfiguration_ShouldRegisterRagChunker()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(CreateConfiguration());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IRagChunker>());
    }

    [Fact]
    public void AddRuniqRagWithConfigurationAndConfigure_ShouldBindOptionsAndApplyProviderOverrides()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Runiq:Rag:DefaultTopK"] = "9",
        });

        services.AddRuniqRag(configuration, rag =>
        {
            rag.UseEmbedding<TestEmbeddingProvider>();
            rag.UseVectorStore<TestVectorStore>();
            rag.UseRetriever<TestRetriever>();
            rag.UseChunker<TestChunker>();
        });

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Equal(9, serviceProvider.GetRequiredService<IOptions<RagOptions>>().Value.DefaultTopK);
        Assert.IsType<TestEmbeddingProvider>(serviceProvider.GetRequiredService<IRagEmbeddingProvider>());
        Assert.IsType<TestVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
        Assert.IsType<TestRetriever>(serviceProvider.GetRequiredService<IRagRetriever>());
        Assert.IsType<TestChunker>(serviceProvider.GetRequiredService<IRagChunker>());
    }

    private sealed class TestEmbeddingProvider : IRagEmbeddingProvider
    {
        public Task<RagEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RagEmbedding());
        }
    }

    private sealed class TestEmbeddingInputPreparer : IRagEmbeddingInputPreparer
    {
        public Task<RagEmbeddingInput> PrepareAsync(
            RagChunk chunk,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RagEmbeddingInput
            {
                Id = chunk.Id,
                ChunkId = chunk.Id,
                DocumentId = chunk.DocumentId,
                Content = chunk.Content,
                ChunkIndex = chunk.Index,
                Metadata = chunk.Metadata,
            });
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

    private static IConfiguration CreateConfiguration(
        IDictionary<string, string?>? values = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();
    }
}
