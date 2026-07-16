namespace Runiq.AI.Rag.Models.Search;

/// <summary>
/// Defines framework-known identifiers for raw vector-search score metrics.
/// </summary>
public static class RagScoreMetrics
{
    /// <summary>
    /// Identifies cosine similarity in the inclusive range from minus one to one, where higher values are better.
    /// </summary>
    public const string CosineSimilarity = "cosine-similarity";

    /// <summary>
    /// Identifies non-negative Euclidean distance, where lower values are better.
    /// </summary>
    public const string EuclideanDistance = "euclidean-distance";

    /// <summary>
    /// Identifies unbounded dot-product similarity, where higher values are better. Dot product has no universal
    /// provider-independent normalization because its bounds depend on the indexed vectors.
    /// </summary>
    public const string DotProduct = "dot-product";
}
