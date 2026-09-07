using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Agents.Validation;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class AgentValidationBoundaryTests
{
    [Fact]
    // Verifies local configuration errors omit credentials and a failed model attempt still allows Codex selection.
    public void ConfigurationFailures_DoNotExposeSensitiveSettings()
    {
        var settings = new ProviderOptions { Url = "relative/path?token=private-url-token" };
        var agent = new Agent("agent", "Agent", "instructions").AddTool<NeverInvokedTool>();
        var tool = Assert.Single(agent.Tools);
        var invalid = Assert.Throws<ArgumentException>(() => agent.UseModel("invalid", "private-api-key", settings));
        Assert.Equal("model", invalid.ParamName);
        Assert.DoesNotContain("private-api-key", invalid.ToString());
        Assert.DoesNotContain("private-url-token", invalid.ToString());
        Assert.Null(agent.Executor);
        Assert.Same(agent, agent.UseCodex());
        Assert.Same(tool, Assert.Single(agent.Tools));
        var duplicate = Assert.Throws<InvalidOperationException>(() => agent.UseModel("openai/model", "private-api-key", settings));
        Assert.Contains(agent.Id, duplicate.Message);
        Assert.DoesNotContain("private-api-key", duplicate.ToString());
        Assert.DoesNotContain("private-url-token", duplicate.ToString());

        var model = new Agent("model-agent", "Model", "instructions", "openai/model", "private-api-key", settings);
        var registration = Assert.Throws<InvalidOperationException>(() => AgentValidator.ValidateRegisteredAgents([model]));
        Assert.Contains(model.Id, registration.Message);
        Assert.Contains("provider url", registration.Message);
        Assert.DoesNotContain("private-api-key", registration.ToString());
        Assert.DoesNotContain("private-url-token", registration.ToString());
        Assert.DoesNotContain(settings.Url, registration.ToString());
    }

    [Theory]
    [InlineData(null, "AgentExecutorMissing")]
    [InlineData(AgentExecutorKind.Codex, "AgentExecutorNotSupported")]
    [InlineData(AgentExecutorKind.Claude, "AgentExecutorNotSupported")]
    // Verifies all existing runtime overloads reject incomplete or unimplemented selections without touching external dependencies.
    public async Task RuntimeOverloads_RejectBeforeAnyExternalWork(AgentExecutorKind? kind, string code)
    {
        var agent = new Agent("agent", "Agent", "instructions").AddTool<NeverInvokedTool>()
            .UseRag(options => options.IndexName = "documents");
        if (kind == AgentExecutorKind.Codex) agent.UseCodex();
        if (kind == AgentExecutorKind.Claude) agent.UseClaude();
        if (kind.HasValue) AgentValidator.ValidateRegisteredAgents([agent]);
        else
        {
            var error = Assert.Throws<InvalidOperationException>(() => AgentValidator.ValidateRegisteredAgents([agent]));
            Assert.Contains(agent.Id, error.Message);
            foreach (var method in new[] { "UseModel", "UseCodex", "UseClaude" }) Assert.Contains(method, error.Message);
        }
        var selected = agent.Executor;
        var dependencies = new ForbiddenDependencies();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var invoker = new AgentToolInvoker(provider);
        var runtime = new AgentExecutionRuntime([agent], dependencies, invoker, dependencies);
        var query = new AgentQuery("question");
        var results = new List<AgentExecutionResult>
        {
            await runtime.ExecuteAsync(agent, "question"),
            await runtime.ExecuteAsync(agent, query),
            await runtime.ExecuteAsync(agent.Id, "question"),
            await runtime.ExecuteAsync(agent.Id, query)
        };
        foreach (var source in new[] { runtime.ExecuteStreamAsync(agent.Id, "question", invoker), runtime.ExecuteStreamAsync(agent.Id, query, invoker) })
        {
            var events = new List<AgentExecutionEvent>();
            await foreach (var item in source) events.Add(item);
            var terminal = Assert.Single(events);
            Assert.Equal(AgentRunStatus.Failed, terminal.Status);
            Assert.Equal(code, terminal.ErrorCode);
            var builder = new AgentExecutionResultBuilder();
            builder.Apply(terminal);
            results.Add(builder.Build());
        }
        Assert.All(results, result =>
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(code, result.ErrorCode);
            Assert.Equal(results[0].ErrorMessage, result.ErrorMessage);
            Assert.Contains(agent.Id, result.ErrorMessage!);
            if (kind.HasValue)
            {
                Assert.Contains(kind.Value.ToString(), result.ErrorMessage!);
                Assert.Contains("no implementation is registered", result.ErrorMessage!);
            }
        });
        Assert.Equal(0, dependencies.Calls);
        Assert.Same(selected, agent.Executor);
    }

    private sealed class ForbiddenDependencies : IChatClientResolver, IRagRetriever
    {
        internal int Calls;
        public IChatClient Resolve(ChatRequest request)
        {
            Calls++;
            throw new InvalidOperationException("Provider resolution must not start.");
        }
        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(RagQuery query, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Retrieval must not start.");
        }
    }

    [RuniqTool("forbidden", "Must never execute in validation tests.")]
    private sealed class NeverInvokedTool : IRuniqTool<string, string>
    {
        public NeverInvokedTool() => throw new InvalidOperationException("Tool activation must not start.");
        public Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Tool execution must not start.");
    }
}
