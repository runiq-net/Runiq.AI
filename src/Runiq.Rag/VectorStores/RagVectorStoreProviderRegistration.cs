using Runiq.Rag.Abstractions.VectorStores;

namespace Runiq.Rag.VectorStores;

/// <summary>
/// Holds the configured provider vector store instance separately from the public validating vector store decorator registration.
/// </summary>
internal sealed class RagVectorStoreProviderRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RagVectorStoreProviderRegistration"/> class.
    /// </summary>
    /// <param name="vectorStore">The provider vector store wrapped by the validating decorator.</param>
    public RagVectorStoreProviderRegistration(IRagVectorStore vectorStore)
    {
        VectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
    }

    /// <summary>
    /// Gets the provider vector store wrapped by the validating decorator.
    /// </summary>
    public IRagVectorStore VectorStore { get; }
}
