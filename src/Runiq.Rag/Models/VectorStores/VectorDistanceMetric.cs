namespace Runiq.Rag.Models.VectorStores;

/// <summary>
/// Specifies the provider-independent distance or similarity metric used by a vector index.
/// </summary>
public enum VectorDistanceMetric
{
    /// <summary>
    /// Uses cosine similarity.
    /// </summary>
    Cosine = 0,

    /// <summary>
    /// Uses dot product similarity.
    /// </summary>
    DotProduct = 1,

    /// <summary>
    /// Uses Euclidean distance.
    /// </summary>
    Euclidean = 2,
}
