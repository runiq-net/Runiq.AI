using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core.Agents;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Core.Tests.Agents;

public sealed class AgentChatApiHandlerTests
{
    [Fact]
    public async Task ChatAsync_ShouldForwardRequestIndexNameToAgentQuery()
    {
        var agent = CreateRagAgent().UseRagIndex("documents");
        var retriever = new TrackingRagRetriever();
        var handler = CreateHandler(agent, retriever);
        var request = new AgentChatRequest(
            Message: "Find travel notes.",
            ResponseMode: AgentChatResponseMode.Result,
            IndexName: "archive");

        await handler.ChatAsync(agent.Id, request, CreateHttpContext(), CancellationToken.None);

        Assert.NotNull(retriever.Query);
        Assert.Equal("archive", retriever.Query.IndexName);
    }

    [Fact]
    public async Task ChatAsync_ShouldLeaveAgentQueryIndexNameNull_WhenRequestIndexNameIsMissing()
    {
        var agent = CreateRagAgent();
        var retriever = new TrackingRagRetriever();
        var handler = CreateHandler(agent, retriever);
        var request = new AgentChatRequest(
            Message: "Find travel notes.",
            ResponseMode: AgentChatResponseMode.Result);

        await handler.ChatAsync(agent.Id, request, CreateHttpContext(), CancellationToken.None);

        Assert.NotNull(retriever.Query);
        Assert.Null(retriever.Query.IndexName);
    }

    [Fact]
    public async Task ChatAsync_ShouldPreserveRuntimeIndexNameOverrideOverAgentRagIndex()
    {
        var agent = CreateRagAgent().UseRagIndex("documents");
        var retriever = new TrackingRagRetriever();
        var handler = CreateHandler(agent, retriever);
        var request = new AgentChatRequest(
            Message: "Find travel notes.",
            ResponseMode: AgentChatResponseMode.Result,
            IndexName: "runtime-override");

        await handler.ChatAsync(agent.Id, request, CreateHttpContext(), CancellationToken.None);

        Assert.Equal("runtime-override", retriever.Query!.IndexName);
    }

    private static AgentChatApiHandler CreateHandler(
        Agent agent,
        IRagRetriever retriever)
    {
        var runtime = new AgentExecutionRuntime(
            agents: [agent],
            openAIResponsesClient: new OpenAIResponsesClient(new HttpClient()),
            openAICompatibleClient: new OpenAICompatibleClient(new HttpClient()),
            toolInvoker: new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()),
            ragRetriever: retriever);

        return new AgentChatApiHandler(runtime);
    }

    private static Agent CreateRagAgent()
    {
        return new Agent(
            id: "travel-agent",
            name: "Travel Agent",
            instructions: "Plan short travel routes.",
            model: "ollama/llama3",
            rag: new AgentRagOptions());
    }

    private static HttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
    }

    private sealed class TrackingRagRetriever : IRagRetriever
    {
        public RagQuery? Query { get; private set; }

        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default)
        {
            Query = query;

            return Task.FromResult<IReadOnlyList<RagSearchResult>>([]);
        }
    }
}

