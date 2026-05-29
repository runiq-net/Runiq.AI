using Runiq.Agents.Configuration;
using Runiq.Agents.Providers;

namespace Runiq.Agents.Tests.Providers;

public sealed class ProviderDefaultsTests
{
    // Verifies that OpenAI provider defaults include the expected protocol, URL, and API key requirement.
    [Fact]
    public void Get_ShouldReturnOpenAIProviderDefaults_WhenProviderIsOpenAI()
    {
        var provider = ProviderDefaults.Get("openai");

        Assert.Equal("openai", provider.ProviderName);
        Assert.Equal(ProviderProtocol.OpenAICompatible, provider.Protocol);
        Assert.Equal("https://api.openai.com/v1", provider.DefaultUrl);
        Assert.True(provider.RequiresApiKey);
    }

    // Verifies that Ollama provider defaults use the local URL and do not require an API key.
    [Fact]
    public void Get_ShouldReturnOllamaProviderDefaults_WhenProviderIsOllama()
    {
        var provider = ProviderDefaults.Get("ollama");

        Assert.Equal("ollama", provider.ProviderName);
        Assert.Equal(ProviderProtocol.Ollama, provider.Protocol);
        Assert.Equal("http://localhost:11434", provider.DefaultUrl);
        Assert.False(provider.RequiresApiKey);
    }

    // Verifies that provider names are normalized before default lookup.
    [Fact]
    public void Get_ShouldNormalizeProviderName_WhenProviderContainsUppercaseCharacters()
    {
        var provider = ProviderDefaults.Get("OpenAI");

        Assert.Equal("openai", provider.ProviderName);
    }

    // Verifies that unsupported provider names produce a clear argument error.
    [Fact]
    public void Get_ShouldThrowArgumentException_WhenProviderIsUnsupported()
    {
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
        // Verifies that empty provider names are not accepted.
        var exception = Assert.Throws<ArgumentException>(() => ProviderDefaults.Get(providerName));

        Assert.Contains("Provider name cannot be empty", exception.Message);
    }

    // Verifies that the provider default URL is used when the agent has no custom provider URL.
    [Fact]
    public void ResolveUrl_ShouldReturnDefaultUrl_WhenAgentDoesNotHaveCustomProviderUrl()
    {
        var agent = new Agent(
            id: "test-agent",
            name: "Test Agent",
            instructions: "Test instructions.",
            model: "openai/gpt-5",
            apiKey: "test-key");

        var url = ProviderDefaults.ResolveUrl(agent);

        Assert.Equal(new Uri("https://api.openai.com/v1"), url);
    }

    // Verifies that an agent-level provider URL overrides the provider default URL.
    [Fact]
    public void ResolveUrl_ShouldReturnCustomUrl_WhenAgentHasProviderUrl()
    {
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

    // Verifies that providers without default URLs require an explicit agent provider URL.
    [Fact]
    public void ResolveUrl_ShouldThrowInvalidOperationException_WhenProviderRequiresCustomUrlButItIsMissing()
    {
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
