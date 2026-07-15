using Runiq.AI.Rag.IngestionSample;

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

        var first = await provider.GenerateAsync("same chunk content");
        var second = await provider.GenerateAsync("same chunk content");

        Assert.Equal(DeterministicSampleEmbeddingProvider.Dimensions, first.Dimensions);
        Assert.Equal(first.Values, second.Values);
    }

    [Fact]
    public async Task DeterministicSampleEmbeddingProvider_ShouldReturnDifferentVectorForDifferentInput()
    {
        var provider = new DeterministicSampleEmbeddingProvider();

        var first = await provider.GenerateAsync("first chunk content");
        var second = await provider.GenerateAsync("second chunk content");

        Assert.NotEqual(first.Values, second.Values);
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

