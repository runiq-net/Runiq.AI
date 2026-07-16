using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Chunking;

namespace Runiq.AI.Rag.Configuration;

/// <summary>Describes a validated, provider-independent RAG index registration.</summary>
public sealed class RagIndexRegistration
{
    internal RagIndexRegistration(string name, IReadOnlyList<IRagDocumentSource> sources, string vectorStoreReference, string embeddingReference, RagChunkingOptions chunking, RagIngestionStartStrategy ingestionStrategy, string embeddingDisplayName, string vectorStoreType, string vectorStoreDisplayName, string? namedVectorStoreReference)
    {
        Name = name;
        Sources = sources;
        VectorStoreReference = vectorStoreReference;
        EmbeddingReference = embeddingReference;
        Chunking = chunking;
        IngestionStrategy = ingestionStrategy;
        EmbeddingDisplayName = embeddingDisplayName;
        VectorStoreType = vectorStoreType;
        VectorStoreDisplayName = vectorStoreDisplayName;
        NamedVectorStoreReference = namedVectorStoreReference;
    }

    /// <summary>Gets the deterministic logical index name.</summary>
    public string Name { get; }

    /// <summary>Gets the document sources associated with the index.</summary>
    public IReadOnlyList<IRagDocumentSource> Sources { get; }

    /// <summary>Gets the effective vector store reference.</summary>
    public string VectorStoreReference { get; }

    /// <summary>Gets the effective embedding model or client reference.</summary>
    public string EmbeddingReference { get; }

    /// <summary>Gets the effective chunking configuration.</summary>
    public RagChunkingOptions Chunking { get; }
    /// <summary>Gets the immutable ingestion start strategy; the default is manual.</summary>
    public RagIngestionStartStrategy IngestionStrategy { get; }
    /// <summary>Gets the safe embedding display name.</summary>
    public string EmbeddingDisplayName { get; }
    /// <summary>Gets the provider-independent vector-store type.</summary>
    public string VectorStoreType { get; }
    /// <summary>Gets the safe vector-store display name.</summary>
    public string VectorStoreDisplayName { get; }
    /// <summary>Gets the named vector-store reference, when one was selected.</summary>
    public string? NamedVectorStoreReference { get; }
}

/// <summary>Builds one named RAG index definition without executing ingestion or provider work.</summary>
public sealed class RagIndexBuilder
{
    private readonly string name;
    private readonly List<IRagDocumentSource> sources = [];
    private string? vectorStoreReference;
    private string? embeddingReference;
    private string? embeddingDisplayName;
    private string? vectorStoreType;
    private string? vectorStoreDisplayName;
    private string? namedVectorStoreReference;
    private RagIngestionStartStrategy? ingestionStrategy;
    private RagChunkingOptions chunking = new();

    internal RagIndexBuilder(string name) => this.name = name;

    /// <summary>Adds a directory-backed document source.</summary>
    /// <param name="rootPath">The directory used when discovery is later executed.</param>
    /// <param name="searchPattern">The file-name search pattern.</param>
    /// <param name="recursive">Whether discovery includes subdirectories.</param>
    /// <returns>The same builder instance.</returns>
    public RagIndexBuilder UseDirectory(string rootPath, string searchPattern = "*", bool recursive = true) =>
        AddSource(new Ingestion.DirectoryRagDocumentSource(rootPath, searchPattern: searchPattern, recursive: recursive));

    /// <summary>Adds a custom document source definition and discovery implementation.</summary>
    /// <param name="source">The source to add.</param>
    /// <returns>The same builder instance.</returns>
    public RagIndexBuilder AddSource(IRagDocumentSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.Identity)) throw new ArgumentException("A document source identity is required.", nameof(source));
        if (sources.Any(candidate => string.Equals(candidate.Identity, source.Identity, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Document source identity '{source.Identity}' is already registered for index '{name}'.");
        sources.Add(source);
        return this;
    }

    /// <summary>Selects the logical vector store registration used by the index.</summary>
    /// <param name="reference">The provider-independent vector store reference.</param>
    /// <returns>The same builder instance.</returns>
    public RagIndexBuilder UseVectorStore(string reference)
    {
        EnsureVectorStoreNotSelected();
        vectorStoreReference = RequireReference(reference, nameof(reference));
        vectorStoreType = "Custom";
        vectorStoreDisplayName = vectorStoreReference;
        return this;
    }

    /// <summary>Selects a validated typed vector-store reference.</summary>
    /// <param name="reference">The typed store selection.</param>
    /// <returns>The same builder instance.</returns>
    public RagIndexBuilder UseVectorStore(RagVectorStoreReference reference)
    {
        if (!reference.IsDefined) throw new ArgumentException("A defined vector-store reference is required.", nameof(reference));
        EnsureVectorStoreNotSelected();
        vectorStoreReference = reference.Reference;
        vectorStoreType = reference.StoreType;
        vectorStoreDisplayName = reference.DisplayName;
        namedVectorStoreReference = reference.NamedReference;
        return this;
    }

    /// <summary>Selects the built-in in-memory vector store without creating it.</summary>
    /// <returns>The same builder instance.</returns>
    public RagIndexBuilder UseInMemoryVectorStore() =>
        UseVectorStore(new RagVectorStoreReference("in-memory", "InMemory", "In-memory vector store"));

    /// <summary>Selects the embedding model or client registration used by the index.</summary>
    /// <param name="reference">The provider/model or named client reference.</param>
    /// <returns>The same builder instance.</returns>
    public RagIndexBuilder UseEmbeddingModel(string reference)
    {
        EnsureEmbeddingNotSelected();
        embeddingReference = RequireReference(reference, nameof(reference));
        embeddingDisplayName = embeddingReference;
        return this;
    }

    /// <summary>Selects a validated typed embedding model through the existing effective-reference path.</summary>
    /// <param name="reference">The typed embedding model.</param>
    /// <returns>The same builder instance.</returns>
    public RagIndexBuilder UseEmbeddingModel(RagEmbeddingModelReference reference)
    {
        if (!reference.IsDefined) throw new ArgumentException("A defined embedding model reference is required.", nameof(reference));
        EnsureEmbeddingNotSelected();
        embeddingReference = reference.Reference;
        embeddingDisplayName = reference.DisplayName;
        return this;
    }

    /// <summary>Configures when ingestion may start; manual is used when this method is not called.</summary>
    /// <param name="configure">The strategy selection callback.</param>
    /// <returns>The same builder instance.</returns>
    public RagIndexBuilder ConfigureIngestion(Action<RagIngestionStrategyBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (ingestionStrategy is not null) throw new InvalidOperationException("An ingestion start strategy has already been configured for this index.");
        var builder = new RagIngestionStrategyBuilder();
        configure(builder);
        ingestionStrategy = builder.Build();
        return this;
    }

    /// <summary>Overrides the chunking settings used by the index.</summary>
    /// <param name="maxChunkLength">The maximum number of characters in a chunk.</param>
    /// <param name="chunkOverlap">The number of characters shared by adjacent chunks.</param>
    /// <returns>The same builder instance.</returns>
    public RagIndexBuilder ConfigureChunking(int maxChunkLength, int chunkOverlap = 100)
    {
        if (maxChunkLength <= 0) throw new ArgumentOutOfRangeException(nameof(maxChunkLength));
        if (chunkOverlap < 0 || chunkOverlap >= maxChunkLength) throw new ArgumentOutOfRangeException(nameof(chunkOverlap));
        chunking = new RagChunkingOptions { MaxChunkLength = maxChunkLength, ChunkOverlap = chunkOverlap };
        return this;
    }

    internal RagIndexRegistration Build()
    {
        if (sources.Count == 0) throw new InvalidOperationException($"RAG index '{name}' must define at least one document source.");
        if (string.IsNullOrWhiteSpace(vectorStoreReference)) throw new InvalidOperationException($"RAG index '{name}' must define a vector store reference.");
        if (string.IsNullOrWhiteSpace(embeddingReference)) throw new InvalidOperationException($"RAG index '{name}' must define an embedding reference.");
        return new RagIndexRegistration(name, sources.AsReadOnly(), vectorStoreReference, embeddingReference, chunking, ingestionStrategy ?? new(RagIngestionStrategyKind.Manual), embeddingDisplayName!, vectorStoreType!, vectorStoreDisplayName!, namedVectorStoreReference);
    }

    private void EnsureVectorStoreNotSelected()
    {
        if (vectorStoreReference is not null) throw new InvalidOperationException($"A vector store has already been selected for index '{name}'.");
    }

    private void EnsureEmbeddingNotSelected()
    {
        if (embeddingReference is not null) throw new InvalidOperationException($"An embedding model has already been selected for index '{name}'.");
    }

    private static string RequireReference(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A non-empty reference is required.", parameterName) : value.Trim();
}
