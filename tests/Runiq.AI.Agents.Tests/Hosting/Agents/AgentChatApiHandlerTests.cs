using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Agents.Tests.TestDoubles;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Agents;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Core.Tests.Agents;

public sealed class AgentChatApiHandlerTests
{
    [Fact]
    public async Task ChatAsync_ShouldForwardRequestIndexNameToAgentQuery()
    {
        var agent = CreateRagAgent().UseRag(options => options.IndexName = "documents");
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
    public async Task ChatAsync_ShouldUseAgentIndex_WhenRequestIndexNameIsMissing()
    {
        var agent = CreateRagAgent().UseRag(options => options.IndexName = "documents");
        var retriever = new TrackingRagRetriever();
        var handler = CreateHandler(agent, retriever);
        var request = new AgentChatRequest(
            Message: "Find travel notes.",
            ResponseMode: AgentChatResponseMode.Result);

        await handler.ChatAsync(agent.Id, request, CreateHttpContext(), CancellationToken.None);

        Assert.NotNull(retriever.Query);
        Assert.Equal("documents", retriever.Query.IndexName);
    }

    [Fact]
    public async Task ChatAsync_ShouldPreserveRuntimeIndexNameOverrideOverAgentRagIndex()
    {
        var agent = CreateRagAgent().UseRag(options => options.IndexName = "documents");
        var retriever = new TrackingRagRetriever();
        var handler = CreateHandler(agent, retriever);
        var request = new AgentChatRequest(
            Message: "Find travel notes.",
            ResponseMode: AgentChatResponseMode.Result,
            IndexName: "runtime-override");

        await handler.ChatAsync(agent.Id, request, CreateHttpContext(), CancellationToken.None);

        Assert.Equal("runtime-override", retriever.Query!.IndexName);
    }

    [Fact]
    // Ensures Agent Chat uses the production Required policy path and exposes early no-context metadata.
    public async Task ChatAsync_RequiredNoContext_ShouldSkipModelAndReturnPolicyOutcome()
    {
        var agent = CreateRagAgent().UseRag(options =>
        {
            options.IndexName = "documents";
            options.Mode = RagExecutionMode.Required;
            options.NoContextBehavior = RagNoContextBehavior.ReturnNotFound;
        });
        var embeddingClient = new EmptyIndexEmbeddingClient();
        var services = new ServiceCollection();
        services.AddRuniqRag();
        services.AddRagEmbeddingClient(_ => embeddingClient);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var retriever = scope.ServiceProvider.GetRequiredService<IRagRetriever>();
        var resolver = new TestChatClientResolver();
        var runtime = new AgentExecutionRuntime(
            agents: [agent],
            chatClientResolver: resolver,
            toolInvoker: new AgentToolInvoker(scope.ServiceProvider),
            ragRetriever: retriever);
        var handler = new AgentChatApiHandler(runtime);

        var result = await handler.ChatAsync(
            agent.Id,
            new AgentChatRequest("Find travel notes.", AgentChatResponseMode.Result),
            new DefaultHttpContext { RequestServices = scope.ServiceProvider },
            CancellationToken.None);

        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var response = Assert.IsType<AgentChatResponse>(valueResult.Value);
        Assert.True(response.IsSuccess);
        Assert.Equal(1, embeddingClient.InvocationCount);
        Assert.Empty(resolver.Requests);
        Assert.True(response.Rag is { ModelInvocationSkipped: true, HasAcceptedContext: false });
        Assert.Equal(RagNoContextReason.NoResults, response.Rag.NoContextReason);
    }

    private static AgentChatApiHandler CreateHandler(
        Agent agent,
        IRagRetriever retriever)
    {
        var runtime = new AgentExecutionRuntime(
            agents: [agent],
            chatClientResolver: new TestChatClientResolver(),
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
            model: "ollama/llama3");
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

    private sealed class EmptyIndexEmbeddingClient : IEmbeddingClient
    {
        public int InvocationCount { get; private set; }

        public Task<EmbeddingResponse> EmbedAsync(
            EmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(new EmbeddingResponse(
                request.Inputs
                    .Select((_, index) => new EmbeddingResult(index, [0.0f], 1))
                    .ToArray()));
        }
    }
}

