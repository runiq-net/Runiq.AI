namespace Runiq.Rag.Configuration;

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
    /// Gets or sets the default number of search results to retrieve.
    /// </summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// Gets or sets the separator used when context content is assembled.
    /// </summary>
    public string ContextSeparator { get; set; } = Environment.NewLine;

    /// <summary>
    /// Gets or sets a value indicating whether empty context is enabled.
    /// </summary>
    public bool EnableEmptyContext { get; set; } = true;
}
