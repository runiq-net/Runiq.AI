using Runiq.Rag.Embeddings;

namespace Runiq.Rag.Tests.Embeddings;

public sealed class NullEmbeddingProviderTests
{
    [Fact]
    public async Task GenerateAsync_ShouldReturnNonNullEmbedding()
    {
        var provider = new NullEmbeddingProvider();

        var embedding = await provider.GenerateAsync("query");

        Assert.NotNull(embedding);
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnEmbeddingWithNonNullValues()
    {
        var provider = new NullEmbeddingProvider();

        var embedding = await provider.GenerateAsync("query");

        Assert.NotNull(embedding.Values);
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnEmptyValues()
    {
        var provider = new NullEmbeddingProvider();

        var embedding = await provider.GenerateAsync("query");

        Assert.Empty(embedding.Values);
    }

    [Fact]
    public async Task GenerateAsync_ShouldNotThrowForEmptyString()
    {
        var provider = new NullEmbeddingProvider();

        var exception = await Record.ExceptionAsync(() => provider.GenerateAsync(string.Empty));

        Assert.Null(exception);
    }
}
