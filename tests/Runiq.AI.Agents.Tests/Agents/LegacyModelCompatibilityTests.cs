using System.Text.Json;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Validation;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.Metadata;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class LegacyModelCompatibilityTests
{
    [Fact]
    // Verifies inherited positional and named base calls retain the legacy constructor without deprecation warnings.
    public void DerivedAgents_PreserveBaseConstructorContract()
    {
        foreach (var agent in new Agent[] { new PositionalAgent(), new NamedAgent() })
        {
            Assert.Equal(AgentExecutorKind.Model, agent.Executor!.Kind);
            Assert.Equal("openai/model", agent.Executor.Model!.Model);
            Assert.Equal("minimal", agent.ReasoningEffort);
            Assert.Equal("low", agent.Verbosity);
            Assert.Null(agent.ApiKey);
            Assert.Null(agent.Provider);
        }
        var constructor = typeof(Agent).GetConstructor([typeof(string), typeof(string), typeof(string), typeof(string),
            typeof(string), typeof(ProviderOptions), typeof(string), typeof(string)])!;
        Assert.False(Attribute.IsDefined(constructor, typeof(ObsoleteAttribute)));
    }

    [Theory]
    [InlineData(null, "minimal", "low", "model")]
    [InlineData("", "minimal", "low", "model")]
    [InlineData("invalid", "minimal", "low", "model")]
    [InlineData("openai/", "minimal", "low", "model")]
    [InlineData("openai/model", null, "low", "ReasoningEffort")]
    [InlineData("openai/model", "invalid", "low", "ReasoningEffort")]
    [InlineData("openai/model", "minimal", null, "Verbosity")]
    [InlineData("openai/model", "minimal", "invalid", "Verbosity")]
    // Verifies historical ArgumentException types and case-sensitive parameter names agree through both definition forms.
    public void InvalidSettings_PreserveLegacyExceptionContract(string? model, string? effort, string? verbosity, string parameter)
    {
        var legacy = Assert.Throws<ArgumentException>(() => new Agent("agent", "Agent", "instructions",
            model!, reasoningEffort: effort!, verbosity: verbosity!));
        var draft = new Agent("agent", "Agent", "instructions");
        var fluent = Assert.Throws<ArgumentException>(() => draft.UseModel(model!, reasoningEffort: effort!, verbosity: verbosity!));
        Assert.Equal(parameter, legacy.ParamName);
        Assert.Equal(parameter, fluent.ParamName);
        Assert.Null(draft.Executor);
    }

    [Theory]
    [InlineData("relative", 10)]
    [InlineData("https://example.invalid", 0)]
    // Verifies provider options remain reference-held and their validation stays at registration for both APIs.
    public void ProviderValidation_RemainsAtRegistration(string url, int seconds)
    {
        var provider = new ProviderOptions { Url = url, Timeout = TimeSpan.FromSeconds(seconds) };
        var legacy = new Agent("agent", "Agent", "instructions", "openai/model", provider: provider);
        var fluent = new Agent("agent", "Agent", "instructions").UseModel("openai/model", provider: provider);
        foreach (var agent in new[] { legacy, fluent })
        {
            Assert.Same(provider, agent.Executor!.Model!.Provider);
            Assert.Throws<InvalidOperationException>(() => AgentValidator.ValidateRegisteredAgents([agent]));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(AgentExecutorKind.Model)]
    [InlineData(AgentExecutorKind.Codex)]
    [InlineData(AgentExecutorKind.Claude)]
    // Verifies safe metadata projection and the complete getter contract without leaking keys or provider configuration.
    public void GetterAndMetadataContract_CoversEverySelection(AgentExecutorKind? kind)
    {
        var agent = new Agent("agent", "Agent", "instructions");
        if (kind == AgentExecutorKind.Model)
            agent.UseModel("openai/model", "secret-api-key", new ProviderOptions { Url = "https://private-provider.invalid" });
        if (kind == AgentExecutorKind.Codex) agent.UseCodex();
        if (kind == AgentExecutorKind.Claude) agent.UseClaude();
        Assert.Equal(kind, agent.Executor?.Kind);
        var getters = new Func<object>[] { () => agent.Model, () => agent.ModelReference, () => agent.ProviderName,
            () => agent.ModelName, () => agent.ReasoningEffort, () => agent.Verbosity };
        if (kind == AgentExecutorKind.Model)
        {
            Assert.All(getters, getter => Assert.NotNull(getter()));
            Assert.Same(agent.Executor!.Model!.ModelReference, agent.ModelReference);
            Assert.Equal("secret-api-key", agent.ApiKey);
        }
        else
        {
            Assert.Null(agent.Executor?.Model);
            Assert.Null(agent.ApiKey);
            Assert.Null(agent.Provider);
            foreach (var getter in getters)
            {
                var failure = Assert.Throws<InvalidOperationException>(() => getter());
                Assert.Contains(agent.Id, failure.Message);
                Assert.Contains("does not have a model executor", failure.Message);
            }
        }
        var metadata = Assert.Single(new RuntimeMetadataService([agent]).GetAgents());
        Assert.Equal(kind == AgentExecutorKind.Model ? "openai/model" : null, metadata.Model);
        Assert.Equal(kind == AgentExecutorKind.Model ? "minimal" : null, metadata.ReasoningEffort);
        Assert.Equal(kind == AgentExecutorKind.Model ? "low" : null, metadata.Verbosity);
        var json = JsonSerializer.Serialize(metadata);
        Assert.DoesNotContain("secret-api-key", json);
        Assert.DoesNotContain("private-provider.invalid", json);
        Assert.DoesNotContain("ApiKey", json);
        Assert.DoesNotContain("Provider", json);
    }

    private sealed class PositionalAgent() : Agent("agent", "Agent", "instructions", "openai/model");
    private sealed class NamedAgent() : Agent(id: "agent", name: "Agent", instructions: "instructions", model: "openai/model");
}
