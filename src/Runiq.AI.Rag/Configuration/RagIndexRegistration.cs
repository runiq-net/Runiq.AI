using Runiq.AI.Rag.Abstractions.Ingestion;
using Runiq.AI.Rag.Chunking;

namespace Runiq.AI.Rag.Configuration;

/// <summary>Describes a validated, provider-independent RAG index registration.</summary>
public sealed class RagIndexRegistration
{
    internal RagIndexRegistration(string name, IReadOnlyList<IRagDocumentSource> sources, string vectorStoreReference, string embeddingReference, RagChunkingOptions chunking)
    {
        Name = name;
        Sources = sources;
        VectorStoreReference = vectorStoreReference;
        EmbeddingReference = embeddingReference;
        Chunking = chunking;
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
}

/// <summary>Builds one named RAG index definition without executing ingestion or provider work.</summary>
public sealed class RagIndexBuilder
{
    private readonly string name;
    private readonly List<IRagDocumentSource> sources = [];
    private string? vectorStoreReference;
    private string? embeddingReference;
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
        vectorStoreReference = RequireReference(reference, nameof(reference));
        return this;
    }

    /// <summary>Selects the embedding model or client registration used by the index.</summary>
    /// <param name="reference">The provider/model or named client reference.</param>
    /// <returns>The same builder instance.</returns>
    public RagIndexBuilder UseEmbeddingModel(string reference)
    {
        embeddingReference = RequireReference(reference, nameof(reference));
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
        return new RagIndexRegistration(name, sources.AsReadOnly(), vectorStoreReference, embeddingReference, chunking);
    }

    private static string RequireReference(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A non-empty reference is required.", parameterName) : value.Trim();
}
