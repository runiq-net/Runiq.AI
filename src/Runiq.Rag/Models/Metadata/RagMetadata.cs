namespace Runiq.Rag.Models.Metadata;

/// <summary>
/// Represents arbitrary string metadata shared by RAG documents, chunks, queries, results, and contexts.
/// </summary>
public sealed class RagMetadata
{
    /// <summary>
    /// Initializes a new empty instance of the <see cref="RagMetadata"/> class.
    /// </summary>
    public RagMetadata()
    {
        Values = new Dictionary<string, string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RagMetadata"/> class by copying the provided values.
    /// </summary>
    /// <param name="values">The metadata values to copy.</param>
    public RagMetadata(IDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Values = new Dictionary<string, string>(values);
    }

    /// <summary>
    /// Gets an empty metadata instance.
    /// </summary>
    public static RagMetadata Empty { get; } = new();

    /// <summary>
    /// Gets the metadata values as string keys and string values.
    /// </summary>
    public IDictionary<string, string> Values { get; }
}
