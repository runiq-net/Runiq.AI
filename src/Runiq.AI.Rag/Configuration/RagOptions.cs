using Runiq.AI.Rag.Chunking;
using Runiq.AI.Core.Configuration;

namespace Runiq.AI.Rag.Configuration;

/// <summary>
/// Provides provider-independent RAG configuration options.
/// </summary>
public sealed class RagOptions
{
    /// <summary>
    /// Defines the configuration section name used for RAG options.
    /// </summary>
    public const string SectionName = "Runiq:Rag";

    /// <summary>
    /// Gets or sets the default number of raw search candidates to retrieve. This value does not guarantee
    /// relevance or acceptance as Agent Chat context.
    /// </summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// Gets or sets the default vector index name used by retrieval queries.
    /// </summary>
    public string? DefaultIndexName { get; set; }

    /// <summary>
    /// Gets or sets the separator used when context content is assembled.
    /// </summary>
    public string ContextSeparator { get; set; } = Environment.NewLine;

    /// <summary>
    /// Gets or sets a value indicating whether empty context is enabled.
    /// </summary>
    public bool EnableEmptyContext { get; set; } = true;

    /// <summary>
    /// Gets or sets provider-independent document chunking options.
    /// </summary>
    public RagChunkingOptions Chunking { get; set; } = new();

    /// <summary>Gets or sets the provider/model reference used for document and query embeddings.</summary>
    public string? EmbeddingModel { get; set; }

    /// <summary>Gets or sets the provider configuration used to resolve named embedding models.</summary>
    public ProviderOptions? EmbeddingProvider { get; set; }
}

