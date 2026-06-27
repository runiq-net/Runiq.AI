using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Abstractions.Retrieval;
using Runiq.Rag.Abstractions.Services;
using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Configuration;
using Runiq.Rag.Embeddings;
using Runiq.Rag.Retrieval;
using Runiq.Rag.Services;
using Runiq.Rag.VectorStores;

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
        services.TryAddSingleton<IRagVectorStore, NullVectorStore>();
        services.TryAddScoped<IRagRetriever, DefaultRetriever>();
        services.TryAddScoped<IRagService, RagService>();
        services.Configure<RagOptions>(_ => { });

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
}
