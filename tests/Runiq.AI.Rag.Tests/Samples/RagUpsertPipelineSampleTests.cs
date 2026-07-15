using Runiq.AI.Rag.UpsertPipelineSample;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;

namespace Runiq.AI.Rag.Tests.Samples;

/// <summary>
/// Guards the developer-facing upsert pipeline sample: the checked-in sample document must stay
/// readable from its expected repository path, and the sample-local deterministic embedding
/// provider must keep producing stable vectors with the advertised dimension count.
/// </summary>
public sealed class RagUpsertPipelineSampleTests
{
    // Verifies that the checked-in sample document the upsert pipeline sample reads at runtime
    // exists at the repository path advertised by the sample document reader.
    [Fact]
    public void SampleDocument_ShouldExistAtExpectedRepositoryPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        var samplePath = Path.Combine(repositoryRoot, SampleDocumentReader.RepositoryRelativePath);

        Assert.True(File.Exists(samplePath), $"Expected sample document at '{samplePath}'.");
    }

    // Verifies that the sample document reader loads the checked-in file content instead of
    // generating document content at runtime, which is a core requirement of the sample.
    [Fact]
    public async Task SampleDocumentReader_ShouldReadCheckedInFileContent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sample = await SampleDocumentReader.ReadAsync(repositoryRoot);
        var normalizedPath = sample.Path.Replace('\\', '/');
        var normalizedExpectedPath = SampleDocumentReader.RepositoryRelativePath.Replace('\\', '/');

        Assert.EndsWith(normalizedExpectedPath, normalizedPath);
        Assert.Contains("Runiq RAG upsert pipeline sample document.", sample.Content, StringComparison.Ordinal);
        Assert.Contains("Expected result:", sample.Content, StringComparison.Ordinal);
    }

    // Verifies that the sample embedding provider returns stable vectors for the same input text,
    // which is what makes the sample's console output and stored records reproducible.
    [Fact]
    public async Task DeterministicSampleEmbeddingProvider_ShouldReturnSameVectorForSameInput()
    {
        var provider = new DeterministicSampleEmbeddingProvider();

        var first = (await provider.EmbedAsync(new EmbeddingRequest(ModelReference.Parse("openai/sample"), ["same chunk content"]))).Results.Single();
        var second = (await provider.EmbedAsync(new EmbeddingRequest(ModelReference.Parse("openai/sample"), ["same chunk content"]))).Results.Single();

        Assert.Equal(first.Vector, second.Vector);
    }

    // Verifies that the sample embedding provider produces vectors with the advertised dimension
    // count, which the sample uses both to create the index and as the expected dimensions for
    // the upsert pipeline's dimension validation.
    [Fact]
    public async Task DeterministicSampleEmbeddingProvider_ShouldProduceAdvertisedDimensionCount()
    {
        var provider = new DeterministicSampleEmbeddingProvider();

        var embedding = (await provider.EmbedAsync(new EmbeddingRequest(ModelReference.Parse("openai/sample"), ["any chunk content"]))).Results.Single();

        Assert.Equal(DeterministicSampleEmbeddingProvider.Dimensions, embedding.Dimensions);
        Assert.Equal(DeterministicSampleEmbeddingProvider.Dimensions, embedding.Vector.Count);
    }

    // Verifies that the sample embedding provider is content-sensitive, so different chunks map
    // to different vectors and the sample output meaningfully differs per chunk.
    [Fact]
    public async Task DeterministicSampleEmbeddingProvider_ShouldReturnDifferentVectorForDifferentInput()
    {
        var provider = new DeterministicSampleEmbeddingProvider();

        var first = (await provider.EmbedAsync(new EmbeddingRequest(ModelReference.Parse("openai/sample"), ["first chunk content"]))).Results.Single();
        var second = (await provider.EmbedAsync(new EmbeddingRequest(ModelReference.Parse("openai/sample"), ["second chunk content"]))).Results.Single();

        Assert.NotEqual(first.Vector, second.Vector);
    }

    /// <summary>
    /// Walks up from the test execution directory to locate the repository root, identified by the
    /// solution file, so path assertions do not depend on the test runner's working directory.
    /// </summary>
    /// <returns>The absolute path of the repository root directory.</returns>
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

