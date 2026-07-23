using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Chunking;

namespace Runiq.AI.Rag.Configuration;

/// <summary>Provides the effective operation-local dependencies for one RAG index.</summary>
public sealed record RagIndexRuntimeConfiguration
{
    /// <summary>Gets the embedding client selected for the index.</summary>
    public required IEmbeddingClient EmbeddingClient { get; init; }
    /// <summary>Gets the provider model passed to embedding requests.</summary>
    public required ModelReference EmbeddingModel { get; init; }
    /// <summary>Gets the vector store selected for ingestion and retrieval.</summary>
    public required IRagVectorStore VectorStore { get; init; }
    /// <summary>Gets an isolated snapshot of the index chunking configuration.</summary>
    public required RagChunkingOptions Chunking { get; init; }
}

/// <summary>Resolves registered index metadata into effective scoped runtime dependencies.</summary>
public interface IRagIndexRuntimeConfigurationResolver
{
    /// <summary>Resolves the effective runtime configuration for an index.</summary>
    /// <param name="indexName">The registered index name, or a legacy unregistered index using global defaults.</param>
    /// <returns>The operation-local effective configuration.</returns>
    RagIndexRuntimeConfiguration Resolve(string indexName);

    /// <summary>Resolves only the vector store for an index without requiring semantic dependencies.</summary>
    /// <param name="indexName">The registered index name, or a legacy unregistered index using global defaults.</param>
    /// <returns>The effective vector store.</returns>
    IRagVectorStore ResolveVectorStore(string indexName) => Resolve(indexName).VectorStore;
}
