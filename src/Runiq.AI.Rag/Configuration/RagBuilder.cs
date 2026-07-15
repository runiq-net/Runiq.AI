using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Rag.Abstractions.Chunking;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Tools;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.DependencyInjection;

namespace Runiq.AI.Rag.Configuration;

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
    /// Replaces the Core embedding client used by RAG with the specified client type.
    /// </summary>
    /// <typeparam name="TClient">The Core embedding client implementation type.</typeparam>
    /// <returns>The same builder instance so calls can be chained.</returns>
    public RagBuilder UseEmbeddingClient<TClient>()
        where TClient : class, IEmbeddingClient
    {
        services.AddRagEmbeddingClient<TClient>();

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
        services.AddRagVectorStore<TVectorStore>();

        return this;
    }

    /// <summary>
    /// Replaces the configured vector store with the in-memory vector store.
    /// </summary>
    /// <returns>The same builder instance so calls can be chained.</returns>
    public RagBuilder UseInMemoryVectorStore()
    {
        services.AddInMemoryRagVectorStore();

        return this;
    }

    /// <summary>
    /// Replaces the configured vector store using the specified factory.
    /// </summary>
    /// <param name="factory">The factory used to create the vector store.</param>
    /// <returns>The same builder instance so calls can be chained.</returns>
    public RagBuilder UseVectorStore(Func<IServiceProvider, IRagVectorStore> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        services.AddRagVectorStore(factory);

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
    /// Replaces the configured retrieval pipeline with the specified pipeline type.
    /// </summary>
    /// <typeparam name="TPipeline">The retrieval pipeline implementation type.</typeparam>
    /// <returns>The same builder instance so calls can be chained.</returns>
    public RagBuilder UseRetrievalPipeline<TPipeline>()
        where TPipeline : class, IRagRetrievalPipeline
    {
        services.Replace(ServiceDescriptor.Scoped<IRagRetrievalPipeline, TPipeline>());

        return this;
    }

    /// <summary>
    /// Replaces the configured Vector Query Tool with the specified tool type.
    /// </summary>
    /// <typeparam name="TTool">The Vector Query Tool implementation type.</typeparam>
    /// <returns>The same builder instance so calls can be chained.</returns>
    public RagBuilder UseVectorQueryTool<TTool>()
        where TTool : class, IVectorQueryTool
    {
        services.Replace(ServiceDescriptor.Scoped<IVectorQueryTool, TTool>());

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
        services.AddRagChunker<TChunker>();

        return this;
    }
}

