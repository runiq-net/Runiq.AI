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
using Runiq.Rag.Services;
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
    public void AddRuniqRag_ShouldRegisterRagDocumentIngestionService()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagDocumentIngestionService));
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
    public void AddRuniqRag_ShouldRegisterRagChunkEmbeddingGenerator()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagChunkEmbeddingGenerator));
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagVectorStore()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagVectorStore));
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagVectorRecordMapper()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagVectorRecordMapper));
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagVectorRecordDimensionValidator()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagVectorRecordDimensionValidator));
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
    public void AddRuniqRag_ShouldResolveRagDocumentIngestionService()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IRagDocumentIngestionService>());
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
    public void AddRuniqRag_ShouldResolveDefaultRagChunkEmbeddingGenerator()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<DefaultRagChunkEmbeddingGenerator>(
            serviceProvider.GetRequiredService<IRagChunkEmbeddingGenerator>());
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveDefaultRagVectorRecordMapper()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<DefaultRagVectorRecordMapper>(
            serviceProvider.GetRequiredService<IRagVectorRecordMapper>());
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveDefaultRagVectorRecordDimensionValidator()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<DefaultRagVectorRecordDimensionValidator>(
            serviceProvider.GetRequiredService<IRagVectorRecordDimensionValidator>());
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveIngestionDependenciesForConsumerScenario()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<DefaultRagChunker>(serviceProvider.GetRequiredService<IRagChunker>());
        Assert.IsType<DefaultRagEmbeddingInputPreparer>(
            serviceProvider.GetRequiredService<IRagEmbeddingInputPreparer>());
        Assert.IsType<DefaultRagChunkEmbeddingGenerator>(
            serviceProvider.GetRequiredService<IRagChunkEmbeddingGenerator>());
        Assert.IsType<DefaultRagDocumentIngestionService>(
            serviceProvider.GetRequiredService<IRagDocumentIngestionService>());
    }

    [Fact]
    public void AddRuniqRag_ShouldNotRegisterProviderSpecificIngestionServices()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        var providerSpecificNames = new[]
        {
            "OpenAI",
            "Azure",
            "Ollama",
            "Pinecone",
            "Qdrant",
            "Weaviate",
        };

        var registeredRagTypes = services
            .SelectMany(descriptor => new[]
            {
                descriptor.ServiceType,
                descriptor.ImplementationType,
                descriptor.ImplementationInstance?.GetType(),
            })
            .Where(type => type is not null && type.Namespace?.StartsWith("Runiq.Rag", StringComparison.Ordinal) == true)
            .Select(type => type!.FullName ?? type.Name);

        Assert.DoesNotContain(
            registeredRagTypes,
            registeredType => providerSpecificNames.Any(
                providerName => registeredType.Contains(providerName, StringComparison.OrdinalIgnoreCase)));
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

        Assert.IsType<ValidatingRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public async Task AddRuniqRag_ShouldValidateDefaultVectorStoreBeforeUpsert()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });
        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records =
            [
                new VectorRecord
                {
                    Id = "vector-1",
                    Values = [0.1f, 0.2f],
                },
            ],
        });

        Assert.False(result.Succeeded);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
        Assert.Equal(3, result.ExpectedDimensions);
        Assert.Equal(2, result.ActualDimensions);
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
    public async Task AddRuniqRag_ShouldWrapUserRegisteredVectorStore()
    {
        var services = new ServiceCollection();
        var vectorStore = new TrackingVectorStore();
        services.AddSingleton<IRagVectorStore>(vectorStore);

        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();
        var resolvedVectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var result = await resolvedVectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            ExpectedDimensions = 3,
            Records =
            [
                new VectorRecord
                {
                    Id = "vector-1",
                    Values = [0.1f, 0.2f],
                },
            ],
        });

        Assert.IsType<ValidatingRagVectorStore>(resolvedVectorStore);
        Assert.False(result.Succeeded);
        Assert.False(vectorStore.UpsertWasCalled);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
    }

    [Fact]
    public void AddRagEmbeddingProvider_ShouldRegisterCustomEmbeddingProvider()
    {
        var services = new ServiceCollection();

        services.AddRagEmbeddingProvider<TestEmbeddingProvider>();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestEmbeddingProvider>(serviceProvider.GetRequiredService<IRagEmbeddingProvider>());
    }

    [Fact]
    public void AddRagEmbeddingProvider_ShouldOverrideDefaultEmbeddingProvider_WhenRegisteredAfterAddRuniqRag()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();
        services.AddRagEmbeddingProvider<TestEmbeddingProvider>();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestEmbeddingProvider>(serviceProvider.GetRequiredService<IRagEmbeddingProvider>());
    }

    [Fact]
    public void AddRagEmbeddingProviderWithFactory_ShouldRegisterCustomEmbeddingProviderInstance()
    {
        var services = new ServiceCollection();
        var provider = new TestEmbeddingProvider();

        services.AddRagEmbeddingProvider(_ => provider);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Same(provider, serviceProvider.GetRequiredService<IRagEmbeddingProvider>());
    }

    [Fact]
    public void AddRagChunker_ShouldRegisterCustomChunker()
    {
        var services = new ServiceCollection();

        services.AddRagChunker<TestChunker>();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestChunker>(serviceProvider.GetRequiredService<IRagChunker>());
    }

    [Fact]
    public void AddRagChunker_ShouldOverrideDefaultChunker_WhenRegisteredAfterAddRuniqRag()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();
        services.AddRagChunker<TestChunker>();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestChunker>(serviceProvider.GetRequiredService<IRagChunker>());
    }

    [Fact]
    public void AddRagChunkerWithFactory_ShouldRegisterCustomChunkerInstance()
    {
        var services = new ServiceCollection();
        var chunker = new TestChunker();

        services.AddRagChunker(_ => chunker);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Same(chunker, serviceProvider.GetRequiredService<IRagChunker>());
    }

    [Fact]
    public void AddInMemoryRagVectorStore_ShouldRegisterInMemoryVectorStore()
    {
        var services = new ServiceCollection();

        services.AddInMemoryRagVectorStore();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<ValidatingRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddInMemoryRagVectorStore_ShouldOverrideDefaultVectorStore_WhenRegisteredAfterAddRuniqRag()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();
        services.AddInMemoryRagVectorStore();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<ValidatingRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddInMemoryRagVectorStore_ShouldNotRequireProviderConfiguration()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(rag => rag.UseInMemoryVectorStore());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<ValidatingRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagService>());
    }

    [Fact]
    public async Task AddInMemoryRagVectorStore_ShouldValidateThroughDecoratorBeforeProviderUpsert()
    {
        var services = new ServiceCollection();
        services.AddInMemoryRagVectorStore();

        using var serviceProvider = services.BuildServiceProvider();
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();

        await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = 3,
        });
        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records =
            [
                new VectorRecord
                {
                    Id = "vector-1",
                    Values = [0.1f, 0.2f],
                },
            ],
        });

        Assert.False(result.Succeeded);
        Assert.Equal("vector-1", result.RecordId);
    }

    [Fact]
    public async Task AddInMemoryRagVectorStore_ShouldFailFast_WhenExpectedDimensionsAreMissing()
    {
        var services = new ServiceCollection();
        services.AddInMemoryRagVectorStore();

        using var serviceProvider = services.BuildServiceProvider();
        var vectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();

        var result = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records =
            [
                new VectorRecord
                {
                    Id = "vector-1",
                    Values = [0.1f, 0.2f],
                },
            ],
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Vector expected dimensions are required for upsert validation.", result.Reason);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
    }

    [Fact]
    public void AddRagVectorStore_ShouldRegisterCustomVectorStore()
    {
        var services = new ServiceCollection();

        services.AddRagVectorStore<TestVectorStore>();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<ValidatingRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public async Task AddRagVectorStoreWithFactory_ShouldWrapCustomVectorStoreInstance()
    {
        var services = new ServiceCollection();
        var vectorStore = new TrackingVectorStore();

        services.AddRagVectorStore(_ => vectorStore);

        using var serviceProvider = services.BuildServiceProvider();
        var resolvedVectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var result = await resolvedVectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            ExpectedDimensions = 3,
            Records =
            [
                new VectorRecord
                {
                    Id = "vector-1",
                    Values = [0.1f, 0.2f],
                },
            ],
        });

        Assert.IsType<ValidatingRagVectorStore>(resolvedVectorStore);
        Assert.False(result.Succeeded);
        Assert.False(vectorStore.UpsertWasCalled);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
    }

    [Fact]
    public void AddRagVectorStore_ShouldAllowTestVectorStoreToBeChanged()
    {
        var services = new ServiceCollection();

        services.AddRagVectorStore<TestVectorStore>();
        services.AddInMemoryRagVectorStore();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<ValidatingRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
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
        Assert.IsType<ValidatingRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagRetriever>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagChunker>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagEmbeddingInputPreparer>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagChunkEmbeddingGenerator>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRagDocumentIngestionService>());
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

        Assert.IsType<ValidatingRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddRuniqRagWithConfigure_ShouldAllowInMemoryVectorStore()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag(rag => rag.UseInMemoryVectorStore());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<ValidatingRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public async Task AddRuniqRagWithConfigure_ShouldWrapCustomVectorStoreFactory()
    {
        var services = new ServiceCollection();
        var vectorStore = new TrackingVectorStore();

        services.AddRuniqRag(rag => rag.UseVectorStore(_ => vectorStore));

        using var serviceProvider = services.BuildServiceProvider();
        var resolvedVectorStore = serviceProvider.GetRequiredService<IRagVectorStore>();
        var result = await resolvedVectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            ExpectedDimensions = 3,
            Records =
            [
                new VectorRecord
                {
                    Id = "vector-1",
                    Values = [0.1f, 0.2f],
                },
            ],
        });

        Assert.IsType<ValidatingRagVectorStore>(resolvedVectorStore);
        Assert.False(result.Succeeded);
        Assert.False(vectorStore.UpsertWasCalled);
        Assert.Equal("documents", result.IndexName);
        Assert.Equal("vector-1", result.RecordId);
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
        Assert.IsType<ValidatingRagVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
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

    private sealed class TrackingVectorStore : IRagVectorStore
    {
        public bool UpsertWasCalled { get; private set; }

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
            UpsertWasCalled = true;

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
