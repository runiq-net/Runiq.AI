using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Configuration;

/// <summary>
/// Configures how raw retrieval candidates become accepted Agent Chat context.
/// </summary>
public sealed class RagResultAcceptanceOptions
{
    /// <summary>
    /// Gets or sets the optional minimum provider-independent relevance in the inclusive range from zero to one.
    /// A null value applies no common relevance threshold, but candidates without normalized relevance still
    /// require <see cref="ProviderSpecificAcceptance"/>.
    /// </summary>
    public double? MinimumRelevance { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of raw candidates requested from retrieval. The default is 20.
    /// This is not an acceptance or relevance guarantee.
    /// </summary>
    public int CandidateCount { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum number of accepted results added to Agent Chat context. The default is 5.
    /// Additional otherwise acceptable candidates are retained as rejected results.
    /// </summary>
    public int MaximumAcceptedResults { get; set; } = 5;

    /// <summary>
    /// Gets or sets an optional provider-specific acceptance predicate for candidates whose raw score cannot be
    /// normalized reliably. Returning <see langword="true"/> accepts the candidate subject to duplicate and result
    /// limits; returning <see langword="false"/> rejects it. The predicate is not used when common relevance exists.
    /// </summary>
    public Func<RagSearchResult, bool>? ProviderSpecificAcceptance { get; set; }
}
