namespace Runiq.AI.Agents;

/// <summary>
/// Describes a retrieval candidate rejected by runtime policy without carrying its chunk content.
/// </summary>
public sealed class RagSearchRejectedResult
{
    /// <summary>Initializes a structured rejected retrieval result.</summary>
    /// <param name="documentId">The source document identifier.</param>
    /// <param name="chunkId">The rejected chunk identifier.</param>
    /// <param name="rawScore">The raw provider score.</param>
    /// <param name="normalizedRelevance">The normalized relevance, when reliably available.</param>
    /// <param name="reason">The reason the candidate was rejected.</param>
    /// <param name="contentPreview">An optional redacted and bounded content preview.</param>
    /// <param name="previewTruncated">Whether the preview was truncated.</param>
    /// <param name="metadata">The bounded application-approved metadata snapshot.</param>
    public RagSearchRejectedResult(string documentId, string chunkId, double rawScore,
        double? normalizedRelevance, RagResultRejectionReason reason, string? contentPreview = null,
        bool previewTruncated = false, IReadOnlyDictionary<string, string>? metadata = null)
    {
        DocumentId = string.IsNullOrWhiteSpace(documentId)
            ? throw new ArgumentException("Document id cannot be null, empty, or whitespace.", nameof(documentId))
            : documentId;
        ChunkId = string.IsNullOrWhiteSpace(chunkId)
            ? throw new ArgumentException("Chunk id cannot be null, empty, or whitespace.", nameof(chunkId))
            : chunkId;
        RawScore = rawScore;
        NormalizedRelevance = normalizedRelevance;
        Reason = Enum.IsDefined(reason)
            ? reason
            : throw new ArgumentOutOfRangeException(nameof(reason), reason, "The rejection reason is not defined.");
        ContentPreview = contentPreview;
        PreviewTruncated = previewTruncated;
        Metadata = metadata is null ? new Dictionary<string, string>() : new Dictionary<string, string>(metadata);
    }

    /// <summary>Gets the source document identifier.</summary>
    public string DocumentId { get; }

    /// <summary>Gets the rejected chunk identifier.</summary>
    public string ChunkId { get; }

    /// <summary>Gets the raw provider score.</summary>
    public double RawScore { get; }

    /// <summary>Gets the normalized relevance, when reliably available.</summary>
    public double? NormalizedRelevance { get; }

    /// <summary>Gets the reason the candidate was rejected.</summary>
    public RagResultRejectionReason Reason { get; }
    /// <summary>Gets the optional redacted and bounded content preview.</summary>
    public string? ContentPreview { get; }
    /// <summary>Gets whether the content preview was truncated.</summary>
    public bool PreviewTruncated { get; }
    /// <summary>Gets the bounded application-approved metadata snapshot.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
