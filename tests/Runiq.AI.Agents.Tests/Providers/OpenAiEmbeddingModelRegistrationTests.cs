using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;

namespace Runiq.AI.Agents.Tests.Providers;

public sealed class OpenAiEmbeddingModelRegistrationTests
{
    // Verifies that the typed small OpenAI model resolves to its provider-visible effective reference.
    [Fact]
    public void UseOpenAiEmbeddingModel_ShouldResolveSmallModel()
    {
        var registration = Register(OpenAiEmbeddingModels.TextEmbedding3Small);

        Assert.Equal("openai/text-embedding-3-small", registration.EmbeddingReference);
        Assert.Equal("OpenAI text-embedding-3-small", registration.EmbeddingDisplayName);
    }

    // Verifies that the typed large OpenAI model resolves to its provider-visible effective reference.
    [Fact]
    public void UseOpenAiEmbeddingModel_ShouldResolveLargeModel() =>
        Assert.Equal("openai/text-embedding-3-large", Register(OpenAiEmbeddingModels.TextEmbedding3Large).EmbeddingReference);

    // Verifies that the OpenAI convenience method rejects a typed reference owned by another provider.
    [Fact]
    public void UseOpenAiEmbeddingModel_ShouldRejectOtherProvider() =>
        Assert.Throws<ArgumentException>(() => Register(new RagEmbeddingModelReference("custom", "model", "Custom")));

    private static RagIndexRegistration Register(RagEmbeddingModelReference model)
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index => index
            .UseDirectory("documents")
            .UseVectorStore("store")
            .UseOpenAiEmbeddingModel(model)));
        using var provider = services.BuildServiceProvider();
        return Assert.Single(provider.GetRequiredService<IRagIndexRegistry>().Registrations);
    }
}
