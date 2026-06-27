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
}
