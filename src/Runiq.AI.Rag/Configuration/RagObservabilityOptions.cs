namespace Runiq.AI.Rag.Configuration;

/// <summary>Configures the safe projection of RAG observability data.</summary>
public sealed class RagObservabilityOptions
{
    /// <summary>Gets the framework-enforced maximum content preview length.</summary>
    public const int MaximumContentPreviewCharacters = 2000;

    /// <summary>Gets the framework-enforced maximum number of projected metadata entries.</summary>
    public const int MaximumMetadataEntries = 32;

    /// <summary>Gets the framework-enforced maximum metadata value length.</summary>
    public const int MaximumMetadataValueCharacters = 512;

    /// <summary>Gets or sets how original and effective queries are exposed.</summary>
    public RagQueryVisibility QueryVisibility { get; set; } = RagQueryVisibility.Visible;

    /// <summary>Gets or sets the value emitted when queries are redacted.</summary>
    public string RedactedQueryPlaceholder { get; set; } = "[REDACTED]";

    /// <summary>Gets or sets the maximum number of text elements emitted for a query.</summary>
    public int MaximumQueryCharacters { get; set; } = 512;

    /// <summary>Gets the case-sensitive metadata keys explicitly approved for observability payloads.</summary>
    public ISet<string> SafeMetadataKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the maximum number of projected metadata entries.</summary>
    public int MetadataEntryLimit { get; set; } = 8;

    /// <summary>Gets or sets the maximum number of text elements in a projected metadata value.</summary>
    public int MaximumMetadataValueLength { get; set; } = 128;

    /// <summary>Gets content preview settings.</summary>
    public RagContentPreviewOptions ContentPreview { get; } = new();
}

/// <summary>Specifies how query text is exposed in RAG observability payloads.</summary>
public enum RagQueryVisibility
{
    /// <summary>Omits query text.</summary>
    Hidden = 0,
    /// <summary>Exposes query text after custom redaction and bounded truncation.</summary>
    Visible = 1,
    /// <summary>Replaces query text with a fixed placeholder.</summary>
    Redacted = 2,
}

/// <summary>Configures bounded, opt-in RAG content previews.</summary>
public sealed class RagContentPreviewOptions
{
    /// <summary>Gets or sets whether previews may be emitted.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the maximum number of text elements in a preview.</summary>
    public int MaximumCharacters { get; set; } = 240;

    /// <summary>Gets or sets whether selected results may include previews.</summary>
    public bool IncludeSelectedResults { get; set; }

    /// <summary>Gets or sets whether rejected results may include previews.</summary>
    public bool IncludeRejectedResults { get; set; }
}
