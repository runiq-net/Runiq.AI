using Runiq.AI.Core.AI.Capabilities;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;
using Runiq.AI.Core.Configuration;

namespace Runiq.AI.Core.Tests.AI;

/// <summary>
/// Verifies independent model capability resolution and preflight validation.
/// </summary>
public sealed class ModelCapabilityTests
{
    // Verifies that multiple independently declared capabilities can be queried without implied features.
    [Fact]
    public void Descriptor_ShouldRequireEveryRequestedCapability()
    {
        var descriptor = new AiModelDescriptor(ModelReference.Parse("ollama/private-model"), ModelCapability.Chat | ModelCapability.Streaming);

        Assert.True(descriptor.Supports(ModelCapability.Chat | ModelCapability.Streaming));
        Assert.False(descriptor.Supports(ModelCapability.ToolCalling));
        Assert.False(descriptor.Supports(ModelCapability.None));
    }

    // Verifies that tool use is rejected before a provider client can be invoked when it is not declared.
    [Fact]
    public void ValidateChat_ShouldRejectUndeclaredToolCalling()
    {
        var model = ModelReference.Parse("ollama/private-model");
        var request = new ChatRequest(model, [new(ChatRole.User, "hello")], Tools: [new("lookup", "Looks up data", "{}")]);

        var exception = Assert.Throws<UnsupportedModelCapabilityException>(() =>
            ModelCapabilityValidator.ValidateChat(new AiModelDescriptor(model, ModelCapability.Chat), request));

        Assert.Equal(ModelCapability.ToolCalling, exception.RequiredCapability);
        Assert.DoesNotContain("key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Verifies that configured fixed embedding dimensions are enforced before an embedding invocation.
    [Fact]
    public void ValidateEmbedding_ShouldRejectConfiguredDimensionMismatch()
    {
        var model = ModelReference.Parse("openai/text-embedding-private");
        var request = new EmbeddingRequest(model, ["input"], Dimensions: 1024);

        Assert.Throws<ArgumentException>(() => ModelCapabilityValidator.ValidateEmbedding(
            new AiModelDescriptor(model, ModelCapability.Embeddings, EmbeddingDimensions: 768), request));
    }

    // Verifies that the conservative OpenAI-compatible default does not claim model-specific features.
    [Fact]
    public void Resolver_ShouldReturnConservativeDefaultsForCustomOllamaModel()
    {
        var descriptor = new DefaultModelCapabilityResolver().Resolve(ModelReference.Parse("ollama/private-model"));

        Assert.True(descriptor.Supports(ModelCapability.Chat));
        Assert.True(descriptor.Supports(ModelCapability.Streaming));
        Assert.False(descriptor.Supports(ModelCapability.ToolCalling));
        Assert.False(descriptor.Supports(ModelCapability.Embeddings));
    }

    // Verifies that an explicit custom-model declaration overrides conservative provider defaults.
    [Fact]
    public void Resolver_ShouldPreferExplicitModelCapabilities()
    {
        var model = ModelReference.Parse("ollama/private-model")
            .WithCapabilities(ModelCapability.Chat | ModelCapability.ToolCalling);

        var descriptor = new DefaultModelCapabilityResolver().Resolve(model);

        Assert.True(descriptor.Supports(ModelCapability.ToolCalling));
        Assert.False(descriptor.Supports(ModelCapability.Streaming));
    }

    // Verifies that embedding capability validation stops the provider client before it can observe a request.
    [Fact]
    public async Task ValidatingEmbeddingClient_ShouldRejectUnsupportedModelBeforeProviderInvocation()
    {
        var provider = new RecordingEmbeddingClient();
        var client = new CapabilityValidatingEmbeddingClient(provider, new DefaultModelCapabilityResolver());
        var request = new EmbeddingRequest(ModelReference.Parse("ollama/private-model"), ["input"]);

        await Assert.ThrowsAsync<UnsupportedModelCapabilityException>(() => client.EmbedAsync(request));

        Assert.False(provider.WasInvoked);
    }

    // Verifies that an embedding-capable model preserves the provider response and request order.
    [Fact]
    public async Task ValidatingEmbeddingClient_ShouldDelegateForEmbeddingCapableModel()
    {
        var provider = new RecordingEmbeddingClient();
        var client = new CapabilityValidatingEmbeddingClient(provider, new DefaultModelCapabilityResolver());
        var model = ModelReference.Parse("ollama/private-model").WithCapabilities(ModelCapability.Embeddings);

        var result = await client.EmbedAsync(new EmbeddingRequest(model, ["first", "second"]));

        Assert.True(provider.WasInvoked);
        Assert.Equal([0, 1], result.Results.Select(result => result.Index));
    }

    // Verifies that a named configuration registration overrides capabilities attached directly to a model reference.
    [Fact]
    public void Configuration_ShouldPreferNamedModelCapabilitiesOverModelReference()
    {
        var provider = new ProviderOptions
        {
            Models = new Dictionary<string, ProviderModelOptions>
            {
                ["chat"] = new() { Model = "private-qwen", Capabilities = [ModelCapability.Chat] }
            }
        };
        var model = ModelReference.Parse("ollama/private-qwen").WithCapabilities(ModelCapability.Embeddings);

        var resolved = ProviderModelReferenceResolver.Resolve(model, provider);

        Assert.Equal(ModelCapability.Chat, resolved.Capabilities);
    }

    // Verifies that an explicitly empty configuration list resolves to no capabilities rather than defaults.
    [Fact]
    public void Configuration_ShouldTreatEmptyCapabilitiesAsExplicitlyUnsupported()
    {
        var provider = new ProviderOptions
        {
            Models = new Dictionary<string, ProviderModelOptions>
            {
                ["private"] = new() { Model = "private-model", Capabilities = [] }
            }
        };

        var resolved = ProviderModelReferenceResolver.Resolve(ModelReference.Parse("ollama/private-model"), provider);

        Assert.Equal(ModelCapability.None, resolved.Capabilities);
    }

    // Verifies that a named registration alias projects the provider-visible model name instead of sending the alias.
    [Fact]
    public void Configuration_ShouldProjectProviderVisibleModelNameFromNamedRegistration()
    {
        var provider = new ProviderOptions
        {
            Models = new Dictionary<string, ProviderModelOptions>
            {
                ["chat"] = new() { Model = "private-qwen", Capabilities = [ModelCapability.Chat] }
            }
        };

        var resolved = ProviderModelReferenceResolver.Resolve(ModelReference.Parse("ollama/chat"), provider);

        Assert.Equal("private-qwen", resolved.ModelName);
    }

    private sealed class RecordingEmbeddingClient : IEmbeddingClient
    {
        public bool WasInvoked { get; private set; }

        public Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            WasInvoked = true;
            return Task.FromResult(new EmbeddingResponse(request.Inputs.Select((_, index) =>
                new EmbeddingResult(index, [1.0f], 1)).ToArray()));
        }
    }
}
