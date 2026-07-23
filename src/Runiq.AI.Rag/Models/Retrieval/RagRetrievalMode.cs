namespace Runiq.AI.Rag.Models.Retrieval;

/// <summary>
/// Selects the retrieval sources used for a RAG query.
/// </summary>
public enum RagRetrievalMode
{
    /// <summary>Uses query embeddings and vector similarity only.</summary>
    Semantic = 0,

    /// <summary>Uses provider lexical search only and does not generate a query embedding.</summary>
    Lexical = 1,

    /// <summary>Uses both semantic and lexical sources and fuses their ranks.</summary>
    Hybrid = 2,
}
