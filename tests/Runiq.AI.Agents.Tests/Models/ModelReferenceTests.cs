using Runiq.AI.Core.Models;

namespace Runiq.AI.Agents.Tests.Models;

public sealed class ModelReferenceTests
{
    // Verifies that provider/model values are split into provider and model names.
    [Fact]
    public void Parse_ShouldSplitProviderAndModel_WhenValueUsesProviderModelFormat()
    {
        var model = ModelReference.Parse("openai/gpt-5");

        Assert.Equal("openai", model.ProviderName);
        Assert.Equal("gpt-5", model.ModelName);
    }

    // Verifies that provider names are normalized for case-insensitive matching.
    [Fact]
    public void Parse_ShouldNormalizeProviderName_WhenProviderContainsUppercaseCharacters()
    {
        var model = ModelReference.Parse("OpenAI/gpt-5");

        Assert.Equal("openai", model.ProviderName);
        Assert.Equal("gpt-5", model.ModelName);
    }

    // Verifies that model names preserve slashes after the first provider separator.
    [Fact]
    public void Parse_ShouldPreserveModelName_WhenModelContainsSlashes()
    {
        var model = ModelReference.Parse("openrouter/meta-llama/llama-3.1-70b-instruct");

        Assert.Equal("openrouter", model.ProviderName);
        Assert.Equal("meta-llama/llama-3.1-70b-instruct", model.ModelName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("gpt-5")]
    [InlineData("/gpt-5")]
    [InlineData("openai/")]
    public void Parse_ShouldThrowArgumentException_WhenValueIsInvalid(string value)
    {
        // Verifies that missing or malformed model references are rejected.
        Assert.Throws<ArgumentException>(() => ModelReference.Parse(value));
    }
}

