namespace Runiq.AI.Rag.Retrieval;

/// <summary>
/// Represents a vector store query failure that must not be treated as an empty retrieval result.
/// </summary>
public sealed class RagVectorStoreQueryException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RagVectorStoreQueryException"/> class.
    /// </summary>
    /// <param name="reason">The provider-independent failure reason.</param>
    /// <param name="indexName">The vector index name used by the failed query.</param>
    public RagVectorStoreQueryException(string reason, string indexName)
        : base(BuildMessage(reason, indexName))
    {
        Reason = reason;
        IndexName = indexName;
    }

    /// <summary>
    /// Gets the provider-independent failure reason.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets the vector index name used by the failed query.
    /// </summary>
    public string IndexName { get; }

    private static string BuildMessage(string reason, string indexName)
    {
        var resolvedReason = string.IsNullOrWhiteSpace(reason)
            ? "Vector store query failed."
            : reason;

        return string.IsNullOrWhiteSpace(indexName)
            ? resolvedReason
            : $"{resolvedReason} IndexName: {indexName}.";
    }
}

