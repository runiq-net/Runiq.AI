namespace Runiq.AI.Agents.Configuration;

/// <summary>
/// Configures deterministic token-budgeted selection of accepted RAG results.
/// </summary>
public sealed class RagContextBudgetOptions
{
    /// <summary>
    /// Gets or sets the maximum combined prompt and response token count. The default is 32,768 tokens.
    /// </summary>
    public int MaximumContextTokens { get; set; } = 32_768;

    /// <summary>
    /// Gets or sets the token capacity reserved for the model response. The default is 4,096 tokens.
    /// </summary>
    public int ResponseTokenReserve { get; set; } = 4_096;

    /// <summary>
    /// Gets or sets the maximum number of selected chunks from one source document. The default is
    /// <see cref="int.MaxValue"/> to preserve existing selection behavior; applications can configure a bounded value.
    /// </summary>
    public int MaximumChunksPerSource { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets a value indicating whether accepted chunks are considered in deterministic source rounds.
    /// Retrieval order determines source priority and order within each source.
    /// </summary>
    public bool PreferSourceDiversity { get; set; }
}
