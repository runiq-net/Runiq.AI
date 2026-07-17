namespace Runiq.AI.Agents;

/// <summary>Identifies one document and chunk pair selected as runtime context.</summary>
public sealed class RagSearchSelectedResult
{
    /// <summary>Initializes a selected RAG search result.</summary>
    /// <param name="documentId">The source document identifier.</param>
    /// <param name="chunkId">The selected chunk identifier.</param>
    /// <param name="rawScore">The raw provider score for this selected candidate.</param>
    /// <param name="normalizedRelevance">The normalized relevance when reliably available.</param>
    /// <param name="metric">The provider-independent score metric when available.</param>
    /// <param name="higherIsBetter">Whether larger raw scores are better for the metric.</param>
    /// <param name="contentPreview">An optional redacted and bounded content preview.</param>
    /// <param name="previewTruncated">Whether the preview was truncated.</param>
    /// <param name="metadata">The bounded application-approved metadata snapshot.</param>
    public RagSearchSelectedResult(string documentId, string chunkId, double rawScore = double.NaN,
        double? normalizedRelevance = null, string? metric = null, bool? higherIsBetter = null,
        string? contentPreview = null, bool previewTruncated = false,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        DocumentId = string.IsNullOrWhiteSpace(documentId) ? throw new ArgumentException("Document id cannot be empty.", nameof(documentId)) : documentId;
        ChunkId = string.IsNullOrWhiteSpace(chunkId) ? throw new ArgumentException("Chunk id cannot be empty.", nameof(chunkId)) : chunkId;
        RawScore = rawScore;
        NormalizedRelevance = normalizedRelevance;
        Metric = metric;
        HigherIsBetter = higherIsBetter;
        ContentPreview = contentPreview;
        PreviewTruncated = previewTruncated;
        Metadata = metadata is null ? new Dictionary<string, string>() : new Dictionary<string, string>(metadata);
    }
    /// <summary>Gets the source document identifier.</summary>
    public string DocumentId { get; }
    /// <summary>Gets the selected chunk identifier.</summary>
    public string ChunkId { get; }
    /// <summary>Gets the raw provider score for this candidate.</summary>
    public double RawScore { get; }
    /// <summary>Gets normalized relevance when reliably available.</summary>
    public double? NormalizedRelevance { get; }
    /// <summary>Gets the score metric when available.</summary>
    public string? Metric { get; }
    /// <summary>Gets whether larger raw scores are better when the metric is known.</summary>
    public bool? HigherIsBetter { get; }
    /// <summary>Gets the optional redacted and bounded content preview.</summary>
    public string? ContentPreview { get; }
    /// <summary>Gets whether the content preview was truncated.</summary>
    public bool PreviewTruncated { get; }
    /// <summary>Gets the bounded application-approved metadata snapshot.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
