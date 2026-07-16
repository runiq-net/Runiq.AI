using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tests.TestDoubles;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core.Agents;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Core.Tests.Agents;

public sealed class RagAgentChatStreamingRegressionTests
{
    [Fact]
    // Ensures RAG lifecycle events are safely deferred until the SSE transport projection is implemented.
    public void FromExecutionEvent_ShouldIgnoreRagSearchLifecycleEvent()
    {
        var executionEvent = AgentExecutionEvent.FromRagSearch(new RagSearchStarted(
            "retrieval-1", "agent-1", "conversation-1", "documents", "question", null, 20));

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Null(streamEvent);
    }

    [Fact]
    // Ensures RAG lifecycle publication cannot turn a successful Agent Chat SSE response into an unsupported failure.
    public async Task ChatAsync_RagEnabledStream_ShouldIgnoreLifecycleEventsWithoutFailure()
    {
        var agent = new Agent(
                id: "travel-agent",
                name: "Travel Agent",
                instructions: "Plan short travel routes.",
                model: "ollama/llama3")
            .UseRag(options => options.IndexName = "documents");
        var services = new ServiceCollection().BuildServiceProvider();
        var runtime = new AgentExecutionRuntime(
            [agent],
            new TestChatClientResolver(),
            new AgentToolInvoker(services),
            new EmptyRetriever());
        var handler = new AgentChatApiHandler(runtime);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() },
        };

        await handler.ChatAsync(
            agent.Id,
            new AgentChatRequest("Find travel notes.", AgentChatResponseMode.Stream),
            httpContext,
            CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();
        Assert.Contains("\"type\":\"assistant_delta\"", payload);
        Assert.Contains("\"type\":\"completed\"", payload);
        Assert.Contains("data: [DONE]", payload);
        Assert.DoesNotContain("Unsupported agent execution event kind", payload);
        Assert.DoesNotContain("\"type\":\"failed\"", payload);
    }

    [Fact]
    // Ensures a RAG-disabled Agent Chat SSE response retains its existing transport event sequence.
    public async Task ChatAsync_RagDisabledStream_ShouldPreserveExistingEvents()
    {
        var agent = new Agent("plain-agent", "Plain Agent", "Help.", "ollama/llama3");
        var services = new ServiceCollection().BuildServiceProvider();
        var runtime = new AgentExecutionRuntime(
            [agent], new TestChatClientResolver(), new AgentToolInvoker(services));
        var handler = new AgentChatApiHandler(runtime);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() },
        };

        await handler.ChatAsync(
            agent.Id,
            new AgentChatRequest("Hello.", AgentChatResponseMode.Stream),
            httpContext,
            CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();
        Assert.Contains("\"type\":\"assistant_delta\"", payload);
        Assert.Contains("\"type\":\"completed\"", payload);
        Assert.DoesNotContain("\"type\":\"failed\"", payload);
    }

    private sealed class EmptyRetriever : IRagRetriever
    {
        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RagSearchResult>>([]);
    }
}
