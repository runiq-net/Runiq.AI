namespace Runiq.Rag.Models.Embeddings;

/// <summary>
/// Represents a vector embedding generated from text content for RAG operations.
/// </summary>
public sealed class RagEmbedding
{
    private IReadOnlyList<float> values = Array.Empty<float>();

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
    public IReadOnlyList<float> Values
    {
        get => values;
        init => values = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the number of dimensions in the embedding vector.
    /// </summary>
    public int Dimensions => Values.Count;
}
