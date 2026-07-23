using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Agents;

/// <summary>Provides safe metadata for an accepted result excluded from model context.</summary>
public sealed record RagSearchContextExcludedResult
{
    /// <summary>Initializes a safe context-selection exclusion projection.</summary>
    /// <param name="documentId">The source document identifier.</param>
    /// <param name="chunkId">The stable chunk identifier.</param>
    /// <param name="reason">The context-selection exclusion reason.</param>
    /// <param name="estimatedTokens">The estimated complete chunk token count.</param>
    /// <param name="provenance">The preserved retrieval provenance.</param>
    public RagSearchContextExcludedResult(
        string documentId,
        string chunkId,
        RagContextSelectionExclusionReason reason,
        int estimatedTokens,
        RagRetrievalProvenance? provenance = null)
    {
        DocumentId = string.IsNullOrWhiteSpace(documentId) ? throw new ArgumentException("A document id is required.", nameof(documentId)) : documentId;
        ChunkId = string.IsNullOrWhiteSpace(chunkId) ? throw new ArgumentException("A chunk id is required.", nameof(chunkId)) : chunkId;
        if (!Enum.IsDefined(reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        if (estimatedTokens < 0) throw new ArgumentOutOfRangeException(nameof(estimatedTokens));
        Reason = reason;
        EstimatedTokens = estimatedTokens;
        Provenance = provenance;
    }

    /// <summary>Gets the source document identifier.</summary>
    public string DocumentId { get; }
    /// <summary>Gets the stable chunk identifier.</summary>
    public string ChunkId { get; }
    /// <summary>Gets the context-selection exclusion reason.</summary>
    public RagContextSelectionExclusionReason Reason { get; }
    /// <summary>Gets the estimated complete chunk token count.</summary>
    public int EstimatedTokens { get; }
    /// <summary>Gets the preserved retrieval provenance.</summary>
    public RagRetrievalProvenance? Provenance { get; }
}
