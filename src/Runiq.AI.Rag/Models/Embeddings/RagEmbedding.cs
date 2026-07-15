namespace Runiq.AI.Rag.Models.Embeddings;

/// <summary>
/// Represents a vector embedding generated from text content for RAG operations.
/// </summary>
public sealed class RagEmbedding
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RagEmbedding"/> class.
    /// </summary>
    public RagEmbedding()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RagEmbedding"/> class with the specified vector values.
    /// </summary>
    /// <param name="values">The embedding vector values.</param>
    public RagEmbedding(IReadOnlyList<float> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Values = values;
    }

    /// <summary>
    /// Gets the embedding vector values.
    /// </summary>
    public IReadOnlyList<float> Values { get; init; } = Array.Empty<float>();

    /// <summary>
    /// Gets the number of dimensions in the embedding vector.
    /// </summary>
    public int Dimensions => Values.Count;
}

