namespace Runiq.AI.Rag.CorporateDocumentAssistant.Models;

/// <summary>
/// Carries a deterministic demo answer and the source chunks used to compose it.
/// </summary>
public sealed class CorporateDocumentQueryResponse
{
    /// <summary>
    /// Gets the question that was submitted by the user.
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Gets the deterministic sample answer composed from retrieved context.
    /// </summary>
    public required string Answer { get; init; }

    /// <summary>
    /// Gets the vector index that was searched.
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// Gets the source chunks used as answer context.
    /// </summary>
    public required IReadOnlyList<CorporateDocumentSourceChunk> Sources { get; init; }
}

