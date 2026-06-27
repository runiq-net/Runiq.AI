using Runiq.Rag.Configuration;

namespace Runiq.Rag.Tests.Configuration;

public sealed class RagOptionsTests
{
    [Fact]
    public void DefaultTopK_ShouldDefaultToFive()
    {
        var options = new RagOptions();

        Assert.Equal(5, options.DefaultTopK);
    }

    [Fact]
    public void ContextSeparator_ShouldDefaultToEnvironmentNewLine()
    {
        var options = new RagOptions();

        Assert.Equal(Environment.NewLine, options.ContextSeparator);
    }

    [Fact]
    public void EnableEmptyContext_ShouldDefaultToTrue()
    {
        var options = new RagOptions();

        Assert.True(options.EnableEmptyContext);
    }

    [Fact]
    public void SectionName_ShouldEqualRuniqRag()
    {
        Assert.Equal("Runiq:Rag", RagOptions.SectionName);
    }

    [Fact]
    public void Chunking_ShouldNotBeNullByDefault()
    {
        var options = new RagOptions();

        Assert.NotNull(options.Chunking);
    }

    [Fact]
    public void ChunkingMaxChunkLength_ShouldDefaultToOneThousand()
    {
        var options = new RagOptions();

        Assert.Equal(1000, options.Chunking.MaxChunkLength);
    }

    [Fact]
    public void ChunkingChunkOverlap_ShouldDefaultToOneHundred()
    {
        var options = new RagOptions();

        Assert.Equal(100, options.Chunking.ChunkOverlap);
    }
}
