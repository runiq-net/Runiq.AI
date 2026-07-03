using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.Rag.Abstractions.Chunking;
using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Abstractions.Retrieval;
using Runiq.Rag.Abstractions.Services;
using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Chunking;
using Runiq.Rag.Configuration;
using Runiq.Rag.Embeddings;
using Runiq.Rag.Retrieval;
using Runiq.Rag.Services;
using Runiq.Rag.VectorStores;
using Runiq.Rag.VectorStores.InMemory;

namespace Runiq.Rag.DependencyInjection;

/// <summary>
/// Provides dependency injection registration methods for RAG services.
/// </summary>
public static class RuniqRagServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default RAG services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add RAG services to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRuniqRag(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IRagEmbeddingProvider, NullEmbeddingProvider>();
        services.TryAddSingleton<IRagEmbeddingInputPreparer, DefaultRagEmbeddingInputPreparer>();
        services.TryAddScoped<IRagChunkEmbeddingGenerator, DefaultRagChunkEmbeddingGenerator>();
        services.TryAddScoped<IRagVectorRecordMapper, DefaultRagVectorRecordMapper>();
        services.TryAddScoped<IRagUpsertVectorRequestMapper, DefaultRagUpsertVectorRequestMapper>();
        services.TryAddSingleton<IRagVectorRecordDimensionValidator, DefaultRagVectorRecordDimensionValidator>();
        services.TryAddDefaultRagVectorStore();
        services.TryAddSingleton<IRagChunker, DefaultRagChunker>();
        services.TryAddScoped<IRagRetriever, DefaultRetriever>();
        services.TryAddScoped<IRagService, RagService>();
        services.TryAddScoped<IRagDocumentIngestionService, DefaultRagDocumentIngestionService>();
        services.Configure<RagOptions>(_ => { });

        return services;
    }

    /// <summary>
    /// Registers the specified RAG embedding provider implementation.
    /// </summary>
    /// <typeparam name="TProvider">The provider-neutral embedding provider implementation type.</typeparam>
    /// <param name="services">The service collection to add the embedding provider to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagEmbeddingProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IRagEmbeddingProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Singleton<IRagEmbeddingProvider, TProvider>());

        return services;
    }

    /// <summary>
    /// Registers a RAG embedding provider using the specified factory.
    /// </summary>
    /// <param name="services">The service collection to add the embedding provider to.</param>
    /// <param name="factory">The factory used to create the embedding provider.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagEmbeddingProvider(
        this IServiceCollection services,
        Func<IServiceProvider, IRagEmbeddingProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        services.Replace(ServiceDescriptor.Singleton(factory));

        return services;
    }

    /// <summary>
    /// Registers the specified RAG chunker implementation.
    /// </summary>
    /// <typeparam name="TChunker">The provider-neutral chunker implementation type.</typeparam>
    /// <param name="services">The service collection to add the chunker to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagChunker<TChunker>(this IServiceCollection services)
        where TChunker : class, IRagChunker
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Singleton<IRagChunker, TChunker>());

        return services;
    }

    /// <summary>
    /// Registers a RAG chunker using the specified factory.
    /// </summary>
    /// <param name="services">The service collection to add the chunker to.</param>
    /// <param name="factory">The factory used to create the chunker.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagChunker(
        this IServiceCollection services,
        Func<IServiceProvider, IRagChunker> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        services.Replace(ServiceDescriptor.Singleton(factory));

        return services;
    }

    /// <summary>
    /// Registers the in-memory RAG vector store.
    /// </summary>
    /// <param name="services">The service collection to add the vector store to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddInMemoryRagVectorStore(this IServiceCollection services)
    {
        return services.AddRagVectorStore<InMemoryRagVectorStore>();
    }

    /// <summary>
    /// Registers the specified RAG vector store implementation.
    /// </summary>
    /// <typeparam name="TVectorStore">The vector store implementation type.</typeparam>
    /// <param name="services">The service collection to add the vector store to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagVectorStore<TVectorStore>(this IServiceCollection services)
        where TVectorStore : class, IRagVectorStore
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ReplaceRagVectorStore(ServiceDescriptor.Singleton<IRagVectorStore, TVectorStore>());

        return services;
    }

    /// <summary>
    /// Registers a RAG vector store using the specified factory.
    /// </summary>
    /// <param name="services">The service collection to add the vector store to.</param>
    /// <param name="factory">The factory used to create the vector store.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRagVectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, IRagVectorStore> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        services.ReplaceRagVectorStore(ServiceDescriptor.Singleton(factory));

        return services;
    }

    /// <summary>
    /// Adds the default RAG services to the dependency injection container and applies fluent configuration.
    /// </summary>
    /// <param name="services">The service collection to add RAG services to.</param>
    /// <param name="configure">The fluent configuration action to apply.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRuniqRag(
        this IServiceCollection services,
        Action<RagBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddRuniqRag();

        var builder = new RagBuilder(services);
        configure(builder);

        return services;
    }

    /// <summary>
    /// Adds the default RAG services to the dependency injection container and binds RAG options from configuration.
    /// </summary>
    /// <param name="services">The service collection to add RAG services to.</param>
    /// <param name="configuration">The configuration source that contains RAG options.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRuniqRag(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddRuniqRag();
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        return services;
    }

    /// <summary>
    /// Adds the default RAG services, binds RAG options from configuration, and applies fluent configuration.
    /// </summary>
    /// <param name="services">The service collection to add RAG services to.</param>
    /// <param name="configuration">The configuration source that contains RAG options.</param>
    /// <param name="configure">The fluent configuration action to apply.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddRuniqRag(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<RagBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddRuniqRag(configuration);

        var builder = new RagBuilder(services);
        configure(builder);

        return services;
    }

    private static void TryAddDefaultRagVectorStore(this IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(RagVectorStoreProviderRegistration)))
        {
            return;
        }

        var existingVectorStoreDescriptor = services.LastOrDefault(
            descriptor => descriptor.ServiceType == typeof(IRagVectorStore));

        if (existingVectorStoreDescriptor is null)
        {
            services.ReplaceRagVectorStore(ServiceDescriptor.Singleton<IRagVectorStore, NullVectorStore>());
            return;
        }

        services.ReplaceRagVectorStore(existingVectorStoreDescriptor);
    }

    private static void ReplaceRagVectorStore(
        this IServiceCollection services,
        ServiceDescriptor vectorStoreDescriptor)
    {
        services.TryAddSingleton<IRagVectorRecordDimensionValidator, DefaultRagVectorRecordDimensionValidator>();
        services.RemoveAll<IRagVectorStore>();
        services.RemoveAll<RagVectorStoreProviderRegistration>();

        services.Add(ServiceDescriptor.Describe(
            typeof(RagVectorStoreProviderRegistration),
            serviceProvider => new RagVectorStoreProviderRegistration(
                CreateVectorStore(vectorStoreDescriptor, serviceProvider)),
            vectorStoreDescriptor.Lifetime));

        services.Add(ServiceDescriptor.Describe(
            typeof(IRagVectorStore),
            serviceProvider => new ValidatingRagVectorStore(
                serviceProvider.GetRequiredService<RagVectorStoreProviderRegistration>().VectorStore,
                serviceProvider.GetRequiredService<IRagVectorRecordDimensionValidator>()),
            vectorStoreDescriptor.Lifetime));
    }

    private static IRagVectorStore CreateVectorStore(
        ServiceDescriptor descriptor,
        IServiceProvider serviceProvider)
    {
        if (descriptor.ImplementationInstance is IRagVectorStore instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IRagVectorStore)descriptor.ImplementationFactory(serviceProvider)!;
        }

        if (descriptor.ImplementationType is not null)
        {
            return (IRagVectorStore)ActivatorUtilities.CreateInstance(
                serviceProvider,
                descriptor.ImplementationType);
        }

        throw new InvalidOperationException("The RAG vector store registration is invalid.");
    }
}
