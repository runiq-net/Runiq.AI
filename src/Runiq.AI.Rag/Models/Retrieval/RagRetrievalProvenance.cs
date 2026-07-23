namespace Runiq.AI.Rag.Models.Retrieval;

/// <summary>
/// Describes how a retrieval result was found and ranked without conflating provider score scales.
/// </summary>
public sealed record RagRetrievalProvenance
{
    /// <summary>Gets the effective retrieval mode.</summary>
    public required RagRetrievalMode Mode { get; init; }

    /// <summary>Gets the one-based semantic source rank, when present.</summary>
    public int? SemanticRank { get; init; }

    /// <summary>Gets the one-based lexical source rank, when present.</summary>
    public int? LexicalRank { get; init; }

    /// <summary>Gets the semantic provider score, when present.</summary>
    public double? SemanticRawScore { get; init; }

    /// <summary>
    /// Gets the lexical provider ranking signal, when present. Lexical values remain provenance and are not
    /// projected into semantic raw-score fields.
    /// </summary>
    public double? LexicalRawScore { get; init; }

    /// <summary>
    /// Gets the reciprocal-rank-fusion score, when hybrid fusion was applied. RRF values remain provenance and
    /// are not projected into semantic raw-score fields.
    /// </summary>
    public double? ReciprocalRankFusionScore { get; init; }

    /// <summary>Gets the one-based final fused rank, when hybrid fusion was applied.</summary>
    public int? FusedRank { get; init; }
}
