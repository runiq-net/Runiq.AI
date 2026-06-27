using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.Rag.Abstractions.Chunking;
using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Abstractions.Retrieval;
using Runiq.Rag.Abstractions.VectorStores;

namespace Runiq.Rag.Configuration;

/// <summary>
/// Provides fluent configuration methods for RAG dependency injection registrations.
/// </summary>
public sealed class RagBuilder
{
    private readonly IServiceCollection services;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection configured by the builder.</param>
    public RagBuilder(IServiceCollection services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Replaces the configured embedding provider with the specified provider type.
    /// </summary>
    /// <typeparam name="TProvider">The embedding provider implementation type.</typeparam>
    /// <returns>The same builder instance so calls can be chained.</returns>
    public RagBuilder UseEmbedding<TProvider>()
        where TProvider : class, IRagEmbeddingProvider
    {
        services.Replace(ServiceDescriptor.Singleton<IRagEmbeddingProvider, TProvider>());

        return this;
    }

    /// <summary>
    /// Replaces the configured vector store with the specified vector store type.
    /// </summary>
    /// <typeparam name="TVectorStore">The vector store implementation type.</typeparam>
    /// <returns>The same builder instance so calls can be chained.</returns>
    public RagBuilder UseVectorStore<TVectorStore>()
        where TVectorStore : class, IRagVectorStore
    {
        services.Replace(ServiceDescriptor.Singleton<IRagVectorStore, TVectorStore>());

        return this;
    }

    /// <summary>
    /// Replaces the configured retriever with the specified retriever type.
    /// </summary>
    /// <typeparam name="TRetriever">The retriever implementation type.</typeparam>
    /// <returns>The same builder instance so calls can be chained.</returns>
    public RagBuilder UseRetriever<TRetriever>()
        where TRetriever : class, IRagRetriever
    {
        services.Replace(ServiceDescriptor.Scoped<IRagRetriever, TRetriever>());

        return this;
    }

    /// <summary>
    /// Replaces the configured chunker with the specified chunker type.
    /// </summary>
    /// <typeparam name="TChunker">The chunker implementation type.</typeparam>
    /// <returns>The same builder instance so calls can be chained.</returns>
    public RagBuilder UseChunker<TChunker>()
        where TChunker : class, IRagChunker
    {
        services.Replace(ServiceDescriptor.Singleton<IRagChunker, TChunker>());

        return this;
    }
}
