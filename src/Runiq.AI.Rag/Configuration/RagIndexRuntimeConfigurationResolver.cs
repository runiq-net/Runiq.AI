using Microsoft.Extensions.Options;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;
using Runiq.AI.Rag.Abstractions.VectorStores;

namespace Runiq.AI.Rag.Configuration;

internal sealed class RagRuntimeProviderRegistry
{
    public Dictionary<string, Func<IServiceProvider, IEmbeddingClient>> Embeddings { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Func<IServiceProvider, IRagVectorStore>> VectorStores { get; } = new(StringComparer.Ordinal);
}

internal sealed class RagIndexRuntimeConfigurationResolver(
    IServiceProvider services,
    IRagIndexRegistry registry,
    RagRuntimeProviderRegistry providers,
    IOptions<RagOptions> defaults) : IRagIndexRuntimeConfigurationResolver
{
    public RagIndexRuntimeConfiguration Resolve(string indexName)
    {
        if (string.IsNullOrWhiteSpace(indexName)) throw new ArgumentException("An index name is required.", nameof(indexName));
        var registration = registry.Registrations.SingleOrDefault(index => string.Equals(index.Name, indexName, StringComparison.Ordinal));
        var embeddingReference = registration?.EmbeddingReference ?? defaults.Value.EmbeddingModel;
        var vectorStoreReference = registration?.VectorStoreReference ?? "default";
        if (string.IsNullOrWhiteSpace(embeddingReference))
            throw new InvalidOperationException($"RAG index '{indexName}' has no effective embedding reference.");
        var embeddingKey = registration is null ? "default" : embeddingReference;
        if (!providers.Embeddings.TryGetValue(embeddingKey, out var embeddingFactory))
            throw new InvalidOperationException($"RAG index '{indexName}' references unregistered embedding '{embeddingReference}'.");
        if (!providers.VectorStores.TryGetValue(vectorStoreReference, out var storeFactory))
            throw new InvalidOperationException($"RAG index '{indexName}' references unregistered vector store '{vectorStoreReference}'.");

        var chunking = registration?.Chunking ?? defaults.Value.Chunking;
        return new RagIndexRuntimeConfiguration
        {
            EmbeddingClient = embeddingFactory(services),
            EmbeddingModel = ModelReference.Parse(embeddingReference),
            VectorStore = storeFactory(services),
            Chunking = new() { MaxChunkLength = chunking.MaxChunkLength, ChunkOverlap = chunking.ChunkOverlap }
        };
    }

    public IRagVectorStore ResolveVectorStore(string indexName)
    {
        if (string.IsNullOrWhiteSpace(indexName)) throw new ArgumentException("An index name is required.", nameof(indexName));
        var registration = registry.Registrations.SingleOrDefault(index => string.Equals(index.Name, indexName, StringComparison.Ordinal));
        var vectorStoreReference = registration?.VectorStoreReference ?? "default";
        if (!providers.VectorStores.TryGetValue(vectorStoreReference, out var storeFactory))
            throw new InvalidOperationException($"RAG index '{indexName}' references unregistered vector store '{vectorStoreReference}'.");
        return storeFactory(services);
    }
}
