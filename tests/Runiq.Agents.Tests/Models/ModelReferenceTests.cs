using Runiq.Agents.Models;

namespace Runiq.Agents.Tests.Models;

public sealed class ModelReferenceTests
{
    [Fact]
    public void Parse_ShouldSplitProviderAndModel_WhenValueUsesProviderModelFormat()
    {
        // Model değeri provider/model formatında verildiğinde provider ve model adlarının doğru ayrıştırıldığını doğrular.
        var model = ModelReference.Parse("openai/gpt-5");

        Assert.Equal("openai", model.ProviderName);
        Assert.Equal("gpt-5", model.ModelName);
    }

    [Fact]
    public void Parse_ShouldNormalizeProviderName_WhenProviderContainsUppercaseCharacters()
    {
        // Provider adının case-insensitive kullanılabilmesi için normalize edildiğini doğrular.
        var model = ModelReference.Parse("OpenAI/gpt-5");

        Assert.Equal("openai", model.ProviderName);
        Assert.Equal("gpt-5", model.ModelName);
    }

    [Fact]
    public void Parse_ShouldPreserveModelName_WhenModelContainsSlashes()
    {
        // Model adının içinde slash bulunması durumunda yalnızca ilk slash üzerinden ayrıştırma yapıldığını doğrular.
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
        // Eksik veya hatalı model formatlarının sessizce kabul edilmediğini doğrular.
        Assert.Throws<ArgumentException>(() => ModelReference.Parse(value));
    }
}