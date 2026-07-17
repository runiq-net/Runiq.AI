using Runiq.AI.Rag.Configuration;

namespace Runiq.AI.Agents.Providers.OpenAI;

/// <summary>Provides discoverable references for OpenAI embedding models supported by Runiq.</summary>
public static class OpenAiEmbeddingModels
{
    /// <summary>Gets the OpenAI text-embedding-3-small model reference.</summary>
    public static RagEmbeddingModelReference TextEmbedding3Small { get; } = new("openai", "text-embedding-3-small", "OpenAI text-embedding-3-small");

    /// <summary>Gets the OpenAI text-embedding-3-large model reference.</summary>
    public static RagEmbeddingModelReference TextEmbedding3Large { get; } = new("openai", "text-embedding-3-large", "OpenAI text-embedding-3-large");
}

/// <summary>Provides OpenAI-specific index configuration conveniences.</summary>
public static class OpenAiRagIndexBuilderExtensions
{
    /// <summary>Selects an OpenAI embedding model without invoking the provider.</summary>
    /// <param name="builder">The index builder.</param>
    /// <param name="model">The typed OpenAI model reference.</param>
    /// <returns>The same index builder.</returns>
    public static RagIndexBuilder UseOpenAiEmbeddingModel(this RagIndexBuilder builder, RagEmbeddingModelReference model)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!string.Equals(model.ProviderName, "openai", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An OpenAI embedding model reference is required.", nameof(model));
        return builder.UseEmbeddingModel(model);
    }
}
