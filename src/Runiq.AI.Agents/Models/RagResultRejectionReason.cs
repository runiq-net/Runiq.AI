namespace Runiq.AI.Agents;

/// <summary>
/// Describes why a raw RAG candidate was not accepted as Agent Chat context.
/// </summary>
public enum RagResultRejectionReason
{
    /// <summary>
    /// The normalized relevance was below the configured threshold or a provider-specific policy rejected it.
    /// </summary>
    BelowMinimumRelevance = 0,

    /// <summary>
    /// The raw score, relevance, metric, or metric direction was invalid.
    /// </summary>
    InvalidScore = 1,

    /// <summary>
    /// The candidate was otherwise acceptable but exceeded the maximum accepted-result count.
    /// </summary>
    ResultLimitExceeded = 2,

    /// <summary>
    /// The candidate content duplicated content from a higher-ranked candidate.
    /// </summary>
    DuplicateContent = 3,

    /// <summary>
    /// The score metric had no common normalization and no provider-specific acceptance policy was configured.
    /// </summary>
    UnsupportedScoreMetric = 4,
}
