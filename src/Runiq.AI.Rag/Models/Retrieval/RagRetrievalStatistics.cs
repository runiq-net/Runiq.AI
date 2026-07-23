namespace Runiq.AI.Rag.Models.Retrieval;

/// <summary>Describes retrieval source and fusion candidate counts before final result limiting.</summary>
public sealed record RagRetrievalStatistics
{
    /// <summary>Gets an empty statistics value.</summary>
    public static RagRetrievalStatistics Empty { get; } = new();

    /// <summary>
    /// Gets the authoritative semantic source candidate count, where zero is a known zero and null means the
    /// retriever did not provide authoritative metadata.
    /// </summary>
    public int? SemanticCandidateCount { get; init; }

    /// <summary>
    /// Gets the authoritative lexical source candidate count, where zero is a known zero and null means the
    /// retriever did not provide authoritative metadata.
    /// </summary>
    public int? LexicalCandidateCount { get; init; }

    /// <summary>
    /// Gets the authoritative duplicate-merged fused candidate count before final limiting, where zero is a
    /// known zero and null means the retriever did not provide authoritative metadata.
    /// </summary>
    public int? FusedCandidateCount { get; init; }
}
