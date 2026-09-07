using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tests.TestDoubles;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Agents.Validation;
using Runiq.AI.Core;
using Runiq.AI.Core.AI.Capabilities;
using Runiq.AI.Core.Models;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.Metadata;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class AgentExecutorTests
{
    [Fact]
    // Ensures fluent model configuration preserves every legacy constructor value and alias.
    public void UseModel_PreservesLegacyConfiguration()
    {
        var provider = new ProviderOptions { Url = "http://localhost:8080" };
        var legacy = new Agent(" support ", " Support ", "instructions", " openai/model ", "key", provider, " HIGH ", " MEDIUM ");
        var fluent = new Agent(" support ", " Support ", "instructions")
            .UseModel(" openai/model ", "key", provider, " HIGH ", " MEDIUM ");

        foreach (var agent in new[] { legacy, fluent })
        {
            Assert.Equal(AgentExecutorKind.Model, agent.Executor!.Kind);
            var model = Assert.IsType<AgentModelConfiguration>(agent.Executor.Model);
            Assert.Equal("support", agent.Id);
            Assert.Equal("Support", agent.Name);
            Assert.Equal("instructions", agent.Instructions);
            Assert.Equal("openai/model", agent.Model);
            Assert.Equal("openai", agent.ProviderName);
            Assert.Equal("model", agent.ModelName);
            Assert.Same(model.ModelReference, agent.ModelReference);
            Assert.Same(provider, agent.Provider);
            Assert.Equal("key", agent.ApiKey);
            Assert.Equal("high", agent.ReasoningEffort);
            Assert.Equal("medium", agent.Verbosity);
            Assert.Throws<InvalidOperationException>(() => agent.UseCodex());
        }
    }

    [Theory]
    [InlineData(AgentExecutorKind.Model)]
    [InlineData(AgentExecutorKind.Codex)]
    [InlineData(AgentExecutorKind.Claude)]
    // Ensures all repeated and cross-executor selections fail without changing identity or attached tools.
    public void Selection_IsExclusiveAndPreservesTools(AgentExecutorKind kind)
    {
        var agent = new Agent("agent", "Agent", "instructions").AddTool<EchoTool>();
        var tool = Assert.Single(agent.Tools);
        Assert.Same(agent, Select(agent, kind));
        var selected = agent.Executor;
        foreach (var alternative in Enum.GetValues<AgentExecutorKind>())
            Assert.Throws<InvalidOperationException>(() => Select(agent, alternative));
        Assert.Same(selected, agent.Executor);
        Assert.Same(tool, Assert.Single(agent.Tools));
        Assert.Equal("agent", agent.Id);
        Assert.Equal("Agent", agent.Name);
        Assert.Equal("instructions", agent.Instructions);
        if (kind != AgentExecutorKind.Model)
        {
            Assert.Null(selected!.Model);
            Assert.Null(agent.Provider);
            Assert.Null(agent.ApiKey);
            Assert.Throws<InvalidOperationException>(() => agent.Model);
            Assert.Throws<InvalidOperationException>(() => agent.ModelReference);
            Assert.Throws<InvalidOperationException>(() => agent.ProviderName);
            Assert.Throws<InvalidOperationException>(() => agent.ModelName);
            Assert.Throws<InvalidOperationException>(() => agent.ReasoningEffort);
            Assert.Throws<InvalidOperationException>(() => agent.Verbosity);
        }
    }

    [Theory]
    [InlineData("", "minimal", "low")]
    [InlineData("invalid", "minimal", "low")]
    [InlineData(null, "minimal", "low")]
    [InlineData("unknown-provider/model", "minimal", "low")]
    [InlineData("openai/", "minimal", "low")]
    [InlineData("openai/model", "invalid", "low")]
    [InlineData("openai/model", "minimal", "invalid")]
    // Ensures invalid model options never consume the agent's single executor selection.
    public void InvalidModel_LeavesSelectionAvailable(string? model, string effort, string verbosity)
    {
        var agent = new Agent("agent", "Agent", "instructions").AddTool<EchoTool>()
            .UseRag(options => options.IndexName = "documents");
        var tool = Assert.Single(agent.Tools);
        var rag = agent.Rag;
        var failure = Assert.Throws<ArgumentException>(() => agent.UseModel(model!, reasoningEffort: effort, verbosity: verbosity));
        Assert.Contains(agent.Id, failure.Message);
        Assert.Contains("invalid executor configuration", failure.Message);
        var cause = Assert.IsType<ArgumentException>(failure.InnerException);
        Assert.Equal(cause.ParamName, failure.ParamName);
        Assert.Null(agent.Executor);
        Assert.Same(tool, Assert.Single(agent.Tools));
        Assert.Same(rag, agent.Rag);
        Assert.Equal("instructions", agent.Instructions);
        Assert.Same(agent, agent.UseClaude());
    }

    [Fact]
    // Ensures competing selection calls cannot publish a second executor configuration.
    public async Task ConcurrentSelections_AllowExactlyOneWinner()
    {
        var agent = new Agent("agent", "Agent", "instructions");
        var attempts = await Task.WhenAll(Enumerable.Range(0, 30).Select(i => Task.Run(() =>
        {
            try { Select(agent, (AgentExecutorKind)(i % 3)); return true; }
            catch (InvalidOperationException) { return false; }
        })));
        Assert.Single(attempts, success => success);
        Assert.NotNull(agent.Executor);
    }

    [Fact]
    // Ensures public configuration APIs cannot construct or mutate an executor outside the validated selection methods.
    public void ExecutorConfiguration_HasNoPublicMutationPath()
    {
        Assert.Null(typeof(Agent).GetProperty(nameof(Agent.Executor))!.SetMethod);
        foreach (var type in new[] { typeof(AgentExecutorConfiguration), typeof(AgentModelConfiguration) })
        {
            Assert.True(type.IsSealed);
            Assert.Empty(type.GetConstructors());
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
        }
    }

    [Fact]
    // Ensures startup rejects incomplete definitions and duplicate identities while allowing all selected executors.
    public void Registration_ValidatesCompletedDefinitions()
    {
        var draft = new Agent("draft", "Draft", null!);
        Assert.Equal(string.Empty, draft.Instructions);
        Assert.Null(draft.Executor);
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddRuniqServer(options => options.AddAgent(draft)));
        Assert.Throws<InvalidOperationException>(() => AgentValidator.ValidateRegisteredAgents([
            new Agent("same", "One", "").UseCodex(), new Agent("SAME", "Two", "").UseClaude()]));

        var agents = Enum.GetValues<AgentExecutorKind>()
            .Select(kind => Select(new Agent(kind.ToString(), kind.ToString(), "instructions"), kind)).ToArray();
        var services = new ServiceCollection();
        services.AddRuniqServer(options => { foreach (var agent in agents) options.AddAgent(agent); });
        using var provider = services.BuildServiceProvider();
        Assert.Equal(agents, provider.GetServices<Agent>());
        var metadata = provider.GetRequiredService<IRuntimeMetadataService>().GetAgents();
        Assert.Equal("openai/model", metadata.Single(agent => agent.Id == "Model").Model);
        Assert.All(metadata.Where(agent => agent.Id != "Model"), agent =>
        {
            Assert.Null(agent.Model);
            Assert.Null(agent.ReasoningEffort);
            Assert.Null(agent.Verbosity);
        });
    }

    [Theory]
    [InlineData("relative", 1)]
    [InlineData("http://localhost", 0)]
    // Ensures fluent model definitions retain startup validation for provider URLs and timeouts.
    public void Registration_RejectsInvalidProviderSettings(string url, int seconds)
    {
        var agent = new Agent("agent", "Agent", "").UseModel("openai/model",
            provider: new ProviderOptions { Url = url, Timeout = TimeSpan.FromSeconds(seconds) });
        Assert.Throws<InvalidOperationException>(() => AgentValidator.ValidateRegisteredAgents([agent]));
    }

    [Theory]
    [InlineData(null, "AgentExecutorMissing")]
    [InlineData(AgentExecutorKind.Codex, "AgentExecutorNotSupported")]
    [InlineData(AgentExecutorKind.Claude, "AgentExecutorNotSupported")]
    // Ensures unsupported and incomplete definitions fail before retrieval or provider resolution in both runtime entry paths.
    public async Task Runtime_RejectsNonModelBeforeRetrieval(AgentExecutorKind? kind, string errorCode)
    {
        var agent = new Agent("agent", "Agent", "instructions")
            .UseRag(options => options.IndexName = "documents");
        if (kind.HasValue) Select(agent, kind.Value);
        var client = new ScriptedChatClient();
        var resolver = new TestChatClientResolver(client);
        using var services = new ServiceCollection().BuildServiceProvider();
        var runtime = new AgentExecutionRuntime([agent], resolver, new AgentToolInvoker(services));
        var result = await runtime.ExecuteAsync(agent, "question");
        Assert.False(result.IsSuccess);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Contains(agent.Id, result.ErrorMessage!);
        if (kind.HasValue)
        {
            Assert.Contains(kind.Value.ToString(), result.ErrorMessage!);
            Assert.Contains("not implemented in this version", result.ErrorMessage!);
        }
        var events = new List<AgentExecutionEvent>();
        await foreach (var item in runtime.ExecuteStreamAsync(agent.Id, "question")) events.Add(item);
        var terminal = Assert.Single(events);
        Assert.Equal(errorCode, terminal.ErrorCode);
        Assert.Equal(result.ErrorMessage, terminal.ErrorMessage);
        Assert.Empty(resolver.Requests);
        Assert.Empty(client.Requests);
    }

    [Fact]
    // Ensures fluent model definitions still execute through the existing provider pipeline.
    public async Task Runtime_ExecutesFluentModel()
    {
        var agent = new Agent("agent", "Agent", "instructions").UseModel("openai/model", "key");
        var client = new ScriptedChatClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var runtime = new AgentExecutionRuntime([agent], new TestChatClientResolver(client), new AgentToolInvoker(services));
        var result = await runtime.ExecuteAsync(agent.Id, "question");
        Assert.True(result.IsSuccess);
        Assert.Single(client.Requests);
    }

    [Fact]
    // Ensures the requested examples compile and preserve defaults with named constructor arguments.
    public void RequestedExamples_PreserveNamedConstructorAndDefaults()
    {
        var modelAgent = new Agent("support", "Support", "Soruları yanıtla.").UseModel("openai/model-name");
        var codexAgent = new Agent("reviewer", "Reviewer", "Kodu incele.").UseCodex();
        var claudeAgent = new Agent("analyst", "Analyst", "Analiz yap.").UseClaude();
        var existingAgent = new Agent(id: "support", name: "Support",
            instructions: "Soruları yanıtla.", model: "openai/model-name");

        Assert.Equal(AgentExecutorKind.Codex, codexAgent.Executor!.Kind);
        Assert.Equal(AgentExecutorKind.Claude, claudeAgent.Executor!.Kind);
        foreach (var agent in new[] { modelAgent, existingAgent })
        {
            Assert.Equal(AgentExecutorKind.Model, agent.Executor!.Kind);
            Assert.Equal("openai/model-name", agent.Model);
            Assert.Null(agent.ApiKey);
            Assert.Null(agent.Provider);
            Assert.Equal("minimal", agent.ReasoningEffort);
            Assert.Equal("low", agent.Verbosity);
        }
    }

    [Fact]
    // Ensures binary constructor lookup, named arguments, and optional defaults remain compatible.
    public void LegacyConstructor_PreservesSignatureAndOptionalDefaults()
    {
        var constructor = typeof(Agent).GetConstructor([
            typeof(string), typeof(string), typeof(string), typeof(string), typeof(string),
            typeof(ProviderOptions), typeof(string), typeof(string)]);
        Assert.NotNull(constructor);
        var parameters = constructor.GetParameters();
        Assert.Equal(new[] { "id", "name", "instructions", "model", "apiKey", "provider", "reasoningEffort", "verbosity" },
            parameters.Select(parameter => parameter.Name));
        Assert.All(parameters.Take(4), parameter => Assert.False(parameter.IsOptional));
        Assert.All(parameters.Skip(4), parameter => Assert.True(parameter.IsOptional));
        Assert.Equal(new object?[] { null, null, "minimal", "low" }, parameters.Skip(4).Select(parameter => parameter.DefaultValue));
    }

    [Theory]
    [InlineData(" OPENAI / model-name ")]
    [InlineData("ollama/library/model:tag")]
    [InlineData("groq/model-name")]
    [InlineData("mistral/model-name")]
    [InlineData("deepseek/model-name")]
    [InlineData("openrouter/organization/model-name")]
    [InlineData("together/organization/model-name")]
    [InlineData("fireworks/accounts/account/models/model-name")]
    [InlineData("nvidia/organization/model-name")]
    [InlineData("azure-openai/deployment-name")]
    // Ensures existing providers and nested names retain Core parser semantics without credentials or endpoints.
    public void ModelDefinitions_AcceptExistingReferenceFormats(string reference)
    {
        var parsed = ModelReference.Parse(reference);
        var constructor = new Agent("agent", "Agent", "instructions", model: reference);
        var fluent = new Agent("agent", "Agent", "instructions").UseModel(reference);
        foreach (var agent in new[] { constructor, fluent })
        {
            Assert.Equal(reference.Trim(), agent.Model);
            Assert.Equal(parsed.ProviderName, agent.ProviderName);
            Assert.Equal(parsed.ModelName, agent.ModelName);
        }
    }

    [Theory]
    [InlineData(AgentExecutorKind.Model)]
    [InlineData(AgentExecutorKind.Codex)]
    [InlineData(AgentExecutorKind.Claude)]
    // Ensures selection composes with subsequent tool and RAG configuration on the original object.
    public void FluentSelection_AllowsFurtherChaining(AgentExecutorKind kind)
    {
        var original = new Agent("agent", "Agent", "instructions");
        var chained = Select(original, kind).AddTool<EchoTool>().UseRag(options => options.IndexName = "documents");
        Assert.Same(original, chained);
        Assert.Single(original.Tools);
        Assert.Equal("documents", original.Rag!.IndexName);
    }

    [Theory]
    [InlineData("minimal", "low")]
    [InlineData("low", "medium")]
    [InlineData("medium", "high")]
    [InlineData(" HIGH ", " LOW ")]
    // Ensures both APIs produce equivalent effective requests including named overrides and generation settings.
    public async Task ModelDefinitions_ProduceEquivalentEffectiveRequests(string effort, string verbosity)
    {
        var provider = new ProviderOptions
        {
            Url = "https://provider.invalid/v1",
            Timeout = TimeSpan.FromSeconds(17),
            Capabilities = [ModelCapability.Chat],
            EmbeddingDimensions = 256,
            Models = new Dictionary<string, ProviderModelOptions>
            {
                ["alias"] = new()
                {
                    Model = "private/actual-model",
                    Capabilities = [ModelCapability.Chat, ModelCapability.Streaming],
                    EmbeddingDimensions = 128
                }
            }
        };
        var constructor = new Agent(id: "agent", name: "Agent", instructions: "instructions",
            model: "openai/alias", apiKey: "unverified-key", provider: provider,
            reasoningEffort: effort, verbosity: verbosity);
        var fluent = new Agent("agent", "Agent", "instructions").UseModel("openai/alias",
            apiKey: "unverified-key", provider: provider, reasoningEffort: effort, verbosity: verbosity);
        var client = new ScriptedChatClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var runtime = new AgentExecutionRuntime([], new TestChatClientResolver(client), new AgentToolInvoker(services));
        foreach (var agent in new[] { constructor, fluent })
        {
            Assert.Same(provider, agent.Executor!.Model!.Provider);
            Assert.Equal(TimeSpan.FromSeconds(17), agent.Provider!.Timeout);
            Assert.True((await runtime.ExecuteAsync(agent, "question")).IsSuccess);
        }
        Assert.Equal(2, client.Requests.Count);
        Assert.All(client.Requests, request =>
        {
            Assert.Equal("openai", request.Model.ProviderName);
            Assert.Equal("private/actual-model", request.Model.ModelName);
            Assert.Equal(ModelCapability.Chat | ModelCapability.Streaming, request.Model.Capabilities);
            Assert.Equal(128, request.Model.EmbeddingDimensions);
            Assert.Equal(new Uri(provider.Url), request.ProviderEndpoint);
            Assert.Equal("unverified-key", request.ApiKey);
            Assert.Equal(effort.Trim().ToLowerInvariant(), request.Options!.ReasoningEffort);
            Assert.Equal(verbosity.Trim().ToLowerInvariant(), request.Options.Verbosity);
        });
    }

    [Theory]
    [InlineData(AgentExecutorKind.Model)]
    [InlineData(AgentExecutorKind.Codex)]
    [InlineData(AgentExecutorKind.Claude)]
    // Ensures repeat selection takes precedence over invalid new options and preserves the original configuration.
    public void InvalidSecondModelSelection_PreservesFirstExecutor(AgentExecutorKind kind)
    {
        var agent = kind == AgentExecutorKind.Model
            ? new Agent("reviewer", "Reviewer", "instructions", model: "openai/model", apiKey: "key")
            : Select(new Agent("reviewer", "Reviewer", "instructions"), kind);
        var original = agent.Executor;
        var failure = Assert.Throws<InvalidOperationException>(() => agent.UseModel("invalid", reasoningEffort: "invalid"));
        Assert.Contains(agent.Id, failure.Message);
        Assert.Contains(kind.ToString(), failure.Message);
        Assert.Contains("Only one executor", failure.Message);
        Assert.Same(original, agent.Executor);
        if (kind == AgentExecutorKind.Model)
        {
            Assert.Equal("openai/model", agent.Model);
            Assert.Equal("key", agent.ApiKey);
            Assert.Equal("minimal", agent.ReasoningEffort);
        }
    }

    [Fact]
    // Ensures missing selections have consistent actionable diagnostics at registration and direct execution boundaries.
    public async Task MissingSelection_UsesConsistentBoundaryDiagnosticsAndCanRecover()
    {
        var agent = new Agent("unfinished", "Unfinished", "instructions");
        var failure = Assert.Throws<InvalidOperationException>(() => AgentValidator.ValidateRegisteredAgents([agent]));
        using var services = new ServiceCollection().BuildServiceProvider();
        var resolver = new TestChatClientResolver();
        var runtime = new AgentExecutionRuntime([agent], resolver, new AgentToolInvoker(services));
        var result = await runtime.ExecuteAsync(agent, "question");
        Assert.Equal("AgentExecutorMissing", result.ErrorCode);
        Assert.Contains(result.ErrorMessage!, failure.Message);
        Assert.Contains(agent.Id, failure.Message);
        Assert.Contains("UseModel, UseCodex, or UseClaude", failure.Message);
        Assert.Null(agent.Executor);
        Assert.Empty(resolver.Requests);

        agent.UseModel("openai/model", "key");
        AgentValidator.ValidateRegisteredAgents([agent]);
        Assert.True((await runtime.ExecuteAsync(agent, "question")).IsSuccess);
    }

    [Fact]
    // Ensures collection does not reject a draft that is completed before the registration callback finishes.
    public void Registration_AllowsSelectionAfterAddingDraft()
    {
        var agent = new Agent("draft", "Draft", "instructions");
        var services = new ServiceCollection();
        services.AddRuniqServer(options =>
        {
            options.AddAgent(agent);
            Assert.Null(agent.Executor);
            agent.UseCodex();
        });
        using var provider = services.BuildServiceProvider();
        Assert.Same(agent, Assert.Single(provider.GetServices<Agent>()));
    }

    private static Agent Select(Agent agent, AgentExecutorKind kind) => kind switch
    {
        AgentExecutorKind.Model => agent.UseModel("openai/model"),
        AgentExecutorKind.Codex => agent.UseCodex(),
        AgentExecutorKind.Claude => agent.UseClaude(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    [RuniqTool(name: "echo", description: "Echoes input.")]
    private sealed class EchoTool : IRuniqTool<string, string>
    {
        public Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default) => Task.FromResult(input);
    }
}
