using Runiq.Agents.Configuration;
using Runiq.Agents.Providers;

namespace Runiq.Agents.Tests.Providers;

public sealed class ProviderDefaultsTests
{
    [Fact]
    public void Get_ShouldReturnOpenAIProviderDefaults_WhenProviderIsOpenAI()
    {
        // OpenAI provider'ının varsayılan bağlantı bilgilerinin doğru tanımlandığını doğrular.
        var provider = ProviderDefaults.Get("openai");

        Assert.Equal("openai", provider.ProviderName);
        Assert.Equal(ProviderProtocol.OpenAICompatible, provider.Protocol);
        Assert.Equal("https://api.openai.com/v1", provider.DefaultUrl);
        Assert.True(provider.RequiresApiKey);
    }

    [Fact]
    public void Get_ShouldReturnOllamaProviderDefaults_WhenProviderIsOllama()
    {
        // Ollama provider'ının local ve API key gerektirmeyen varsayılanlarla tanımlandığını doğrular.
        var provider = ProviderDefaults.Get("ollama");

        Assert.Equal("ollama", provider.ProviderName);
        Assert.Equal(ProviderProtocol.Ollama, provider.Protocol);
        Assert.Equal("http://localhost:11434", provider.DefaultUrl);
        Assert.False(provider.RequiresApiKey);
    }

    [Fact]
    public void Get_ShouldNormalizeProviderName_WhenProviderContainsUppercaseCharacters()
    {
        // Provider adının büyük/küçük harften bağımsız çözümlendiğini doğrular.
        var provider = ProviderDefaults.Get("OpenAI");

        Assert.Equal("openai", provider.ProviderName);
    }

    [Fact]
    public void Get_ShouldThrowArgumentException_WhenProviderIsUnsupported()
    {
        // Desteklenmeyen provider adlarının sessizce kabul edilmediğini doğrular.
        var exception = Assert.Throws<ArgumentException>(() => ProviderDefaults.Get("unknown-provider"));

        Assert.Contains("Unsupported model provider", exception.Message);
        Assert.Contains("openai", exception.Message);
        Assert.Contains("ollama", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Get_ShouldThrowArgumentException_WhenProviderNameIsEmpty(string providerName)
    {
        // Boş provider adlarının geçerli kabul edilmediğini doğrular.
        var exception = Assert.Throws<ArgumentException>(() => ProviderDefaults.Get(providerName));

        Assert.Contains("Provider name cannot be empty", exception.Message);
    }

    [Fact]
    public void ResolveUrl_ShouldReturnDefaultUrl_WhenAgentDoesNotHaveCustomProviderUrl()
    {
        // Agent üzerinde özel URL verilmediğinde provider'ın varsayılan URL değerinin kullanıldığını doğrular.
        var agent = new Agent(
            id: "test-agent",
            name: "Test Agent",
            instructions: "Test instructions.",
            model: "openai/gpt-5",
            apiKey: "test-key");

        var url = ProviderDefaults.ResolveUrl(agent);

        Assert.Equal(new Uri("https://api.openai.com/v1"), url);
    }

    [Fact]
    public void ResolveUrl_ShouldReturnCustomUrl_WhenAgentHasProviderUrl()
    {
        // Agent üzerinde özel provider URL verilirse varsayılan URL yerine bu değerin kullanıldığını doğrular.
        var agent = new Agent(
            id: "test-agent",
            name: "Test Agent",
            instructions: "Test instructions.",
            model: "openai/gpt-5",
            apiKey: "test-key",
            provider: new ProviderOptions
            {
                Url = "https://custom.provider.local/v1"
            });

        var url = ProviderDefaults.ResolveUrl(agent);

        Assert.Equal(new Uri("https://custom.provider.local/v1"), url);
    }

    [Fact]
    public void ResolveUrl_ShouldThrowInvalidOperationException_WhenProviderRequiresCustomUrlButItIsMissing()
    {
        // Varsayılan URL tanımlanmayan provider'larda özel URL verilmezse açık hata üretildiğini doğrular.
        var agent = new Agent(
            id: "azure-agent",
            name: "Azure Agent",
            instructions: "Test instructions.",
            model: "azure-openai/gpt-5",
            apiKey: "test-key");

        var exception = Assert.Throws<InvalidOperationException>(() => ProviderDefaults.ResolveUrl(agent));

        Assert.Contains("Provider.Url is missing", exception.Message);
        Assert.Contains("azure-agent", exception.Message);
    }
}