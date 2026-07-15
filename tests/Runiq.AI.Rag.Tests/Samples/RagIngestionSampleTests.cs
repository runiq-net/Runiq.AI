using Runiq.AI.Rag.IngestionSample;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;

namespace Runiq.AI.Rag.Tests.Samples;

public sealed class RagIngestionSampleTests
{
    [Fact]
    public void SampleDocument_ShouldExistAtExpectedRepositoryPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        var samplePath = Path.Combine(repositoryRoot, SampleDocumentReader.RepositoryRelativePath);

        Assert.True(File.Exists(samplePath), $"Expected sample document at '{samplePath}'.");
    }

    [Fact]
    public async Task SampleDocumentReader_ShouldReadCheckedInFileContent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sample = await SampleDocumentReader.ReadAsync(repositoryRoot);
        var normalizedPath = sample.Path.Replace('\\', '/');
        var normalizedExpectedPath = SampleDocumentReader.RepositoryRelativePath.Replace('\\', '/');

        Assert.EndsWith(normalizedExpectedPath, normalizedPath);
        Assert.Contains("Runiq RAG ingestion sample document.", sample.Content, StringComparison.Ordinal);
        Assert.Contains("Expected result:", sample.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeterministicSampleEmbeddingProvider_ShouldReturnSameVectorForSameInput()
    {
        var provider = new DeterministicSampleEmbeddingProvider();

        var first = (await provider.EmbedAsync(new EmbeddingRequest(ModelReference.Parse("openai/sample"), ["same chunk content"]))).Results.Single();
        var second = (await provider.EmbedAsync(new EmbeddingRequest(ModelReference.Parse("openai/sample"), ["same chunk content"]))).Results.Single();

        Assert.Equal(DeterministicSampleEmbeddingProvider.Dimensions, first.Dimensions);
        Assert.Equal(first.Vector, second.Vector);
    }

    [Fact]
    public async Task DeterministicSampleEmbeddingProvider_ShouldReturnDifferentVectorForDifferentInput()
    {
        var provider = new DeterministicSampleEmbeddingProvider();

        var first = (await provider.EmbedAsync(new EmbeddingRequest(ModelReference.Parse("openai/sample"), ["first chunk content"]))).Results.Single();
        var second = (await provider.EmbedAsync(new EmbeddingRequest(ModelReference.Parse("openai/sample"), ["second chunk content"]))).Results.Single();

        Assert.NotEqual(first.Vector, second.Vector);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Runiq.AI.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}

