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
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Runiq.AI.Core.Tests.Agents;

public sealed class RagAgentChatStreamingRegressionTests
{
    // Ensures blocked readiness is equivalent across real SSE serialization and the non-streaming Agent Chat response.
    [Fact]
    public async Task ChatAsync_NotInitialized_ShouldSerializeStructuredReadinessInBothModes()
    {
        var agent = new Agent("rag-agent", "RAG Agent", "Help.", "ollama/llama3").UseRag(options => options.IndexName = "documents");
        var services = new ServiceCollection().BuildServiceProvider();
        var runtime = CreateReadinessRuntime(agent, services, new RagIndexRuntimeStatus { IndexName = "documents", Readiness = RagIndexReadiness.NotInitialized, LastUpdatedAt = DateTimeOffset.UtcNow });
        var handler = new AgentChatApiHandler(runtime);
        var streamContext = new DefaultHttpContext { RequestServices = services, Response = { Body = new MemoryStream() } };

        await handler.ChatAsync(agent.Id, new AgentChatRequest("Find notes.", AgentChatResponseMode.Stream), streamContext, CancellationToken.None);
        streamContext.Response.Body.Position = 0;
        var sse = await new StreamReader(streamContext.Response.Body, Encoding.UTF8).ReadToEndAsync();
        var startedIndex = sse.IndexOf("\"type\":\"rag_search_started\"", StringComparison.Ordinal);
        var blockedIndex = sse.IndexOf("\"type\":\"rag_search_blocked\"", StringComparison.Ordinal);
        var terminalIndex = sse.IndexOf("\"type\":\"failed\"", StringComparison.Ordinal);

        Assert.True(startedIndex >= 0 && blockedIndex > startedIndex && terminalIndex > blockedIndex);
        Assert.Contains("\"readiness\":\"NotInitialized\"", sse);
        Assert.Contains("\"suggestedAction\":\"StartIngestion\"", sse);
        Assert.DoesNotContain("rag_search_failed", sse);
        Assert.DoesNotContain("rag_search_completed", sse);
        Assert.DoesNotContain("assistant_delta", sse);
        Assert.DoesNotContain("citations", sse);
        Assert.Contains("data: [DONE]", sse);

        var result = await handler.ChatAsync(agent.Id, new AgentChatRequest("Find notes.", AgentChatResponseMode.Result),
            new DefaultHttpContext { RequestServices = services }, CancellationToken.None);
        var response = Assert.IsType<AgentChatResponse>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("documents", response.RagReadiness?.IndexName);
        Assert.Equal(RagIndexReadiness.NotInitialized, response.RagReadiness?.Readiness);
        Assert.Equal(RagReadinessSuggestedAction.StartIngestion, response.RagReadiness?.SuggestedAction);
        Assert.Null(response.GroundingEvidence);
        Assert.Null(response.Citations);
    }
    // Ensures result-mode responses reuse the completed lifecycle projection without serializing raw RAG metadata or chunk content.
    [Fact]
    public async Task ChatAsync_RagEnabledResult_ShouldProjectContentFreeGroundingEvidence()
    {
        var agent = new Agent("rag-agent", "RAG Agent", "Help.", "ollama/llama3")
            .UseRag(options =>
            {
                options.IndexName = "documents";
                options.NoContextBehavior = RagNoContextBehavior.ReturnNotFound;
            });
        var services = new ServiceCollection().BuildServiceProvider();
        var runtime = new AgentExecutionRuntime(
            [agent],
            new TestChatClientResolver(),
            new AgentToolInvoker(services),
            new EmptyRetriever());
        var handler = new AgentChatApiHandler(runtime);

        var result = await handler.ChatAsync(
            agent.Id,
            new AgentChatRequest("Find notes.", AgentChatResponseMode.Result),
            new DefaultHttpContext { RequestServices = services },
            CancellationToken.None);

        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var response = Assert.IsType<AgentChatResponse>(valueResult.Value);
        Assert.True(response.IsSuccess, response.ErrorMessage);
        var evidence = Assert.Single(response.GroundingEvidence!);
        Assert.Empty(evidence.SelectedResults!);
        Assert.Equal(RagNoContextReason.NoResults, evidence.NoContextReason);

        var payload = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("groundingEvidence", payload);
        Assert.DoesNotContain("\"rag\"", payload);
        Assert.DoesNotContain("\"content\":\"content\"", payload);
    }

    [Fact]
    // Ensures RAG lifecycle events are projected instead of being silently ignored by the SSE transport.
    public void FromExecutionEvent_ShouldProjectRagSearchLifecycleEvent()
    {
        var executionEvent = AgentExecutionEvent.FromRagSearch(new RagSearchStarted(
            "retrieval-1", "agent-1", "conversation-1", "documents", "question", null, 20));

        var streamEvent = AgentChatStreamEventMapper.FromExecutionEvent(executionEvent);

        Assert.Equal("rag_search_started", streamEvent.Type);
        Assert.Equal("retrieval-1", streamEvent.RagSearch!.CorrelationId);
    }

    [Fact]
    // Ensures a successful Agent Chat SSE response preserves RAG lifecycle order before model events.
    public async Task ChatAsync_RagEnabledStream_ShouldProjectLifecycleBeforeModelEvents()
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
        var startedIndex = payload.IndexOf("\"type\":\"rag_search_started\"", StringComparison.Ordinal);
        var completedIndex = payload.IndexOf("\"type\":\"rag_search_completed\"", StringComparison.Ordinal);
        var tokenIndex = payload.IndexOf("\"type\":\"assistant_delta\"", StringComparison.Ordinal);
        Assert.True(startedIndex >= 0);
        Assert.True(completedIndex > startedIndex);
        Assert.True(tokenIndex > completedIndex);
        Assert.Contains("\"noContextReason\":\"NoResults\"", payload);
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

    [Fact]
    // Ensures failed RAG lifecycle and terminal agent failure remain separate ordered SSE events.
    public async Task ChatAsync_RagFailureStream_ShouldProjectLifecycleFailureBeforeTerminalFailure()
    {
        var agent = new Agent("rag-agent", "RAG Agent", "Help.", "ollama/llama3")
            .UseRag(options => options.IndexName = "documents");
        var services = new ServiceCollection().BuildServiceProvider();
        var runtime = new AgentExecutionRuntime(
            [agent],
            new TestChatClientResolver(),
            new AgentToolInvoker(services),
            new ThrowingRetriever());
        var handler = new AgentChatApiHandler(runtime);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() },
        };

        await handler.ChatAsync(
            agent.Id,
            new AgentChatRequest("Find notes.", AgentChatResponseMode.Stream),
            httpContext,
            CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();
        var startedIndex = payload.IndexOf("\"type\":\"rag_search_started\"", StringComparison.Ordinal);
        var ragFailedIndex = payload.IndexOf("\"type\":\"rag_search_failed\"", StringComparison.Ordinal);
        var terminalFailedIndex = payload.IndexOf("\"type\":\"failed\"", StringComparison.Ordinal);
        Assert.True(startedIndex >= 0);
        Assert.True(ragFailedIndex > startedIndex);
        Assert.True(terminalFailedIndex > ragFailedIndex);
        Assert.Contains("\"failureClassification\":\"EmbeddingFailed\"", payload);
        Assert.DoesNotContain("provider-secret", payload);
        Assert.DoesNotContain("\"type\":\"rag_search_completed\"", payload);
    }

    [Fact]
    // Ensures the real Agent Chat serializer safely omits non-finite rejected scores and completes the SSE stream.
    public async Task ChatAsync_InvalidNormalizedRelevance_ShouldSerializeCompletedLifecycle()
    {
        var agent = new Agent("rag-agent", "RAG Agent", "Help.", "ollama/llama3")
            .UseRag(options => options.IndexName = "documents");
        var services = new ServiceCollection().BuildServiceProvider();
        var runtime = new AgentExecutionRuntime(
            [agent],
            new TestChatClientResolver(),
            new AgentToolInvoker(services),
            new InvalidScoreRetriever());
        var handler = new AgentChatApiHandler(runtime);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() },
        };

        await handler.ChatAsync(
            agent.Id,
            new AgentChatRequest("Find notes.", AgentChatResponseMode.Stream),
            httpContext,
            CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();
        var completedLifecycle = payload
            .Split("\n", StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.Contains("\"type\":\"rag_search_completed\"", StringComparison.Ordinal));
        Assert.Contains("\"type\":\"rag_search_completed\"", payload);
        Assert.Contains("\"reason\":\"InvalidScore\"", payload);
        Assert.DoesNotContain("normalizedRelevance", completedLifecycle);
        Assert.DoesNotContain("NaN", payload);
        Assert.DoesNotContain("Infinity", payload);
        Assert.DoesNotContain("\"rag\":", payload);
        Assert.DoesNotContain("\"content\":\"content\"", payload);
        Assert.Contains("data: [DONE]", payload);
    }

    private sealed class EmptyRetriever : IRagRetriever
    {
        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RagSearchResult>>([]);
    }

    private static AgentExecutionRuntime CreateReadinessRuntime(Agent agent, IServiceProvider services, RagIndexRuntimeStatus status)
    {
        var builder = (RagIndexBuilder)Activator.CreateInstance(typeof(RagIndexBuilder), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, [status.IndexName], null)!;
        builder.UseDirectory("documents").UseVectorStore("store").UseEmbeddingModel("model");
        var registration = (RagIndexRegistration)typeof(RagIndexBuilder).GetMethod("Build", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(builder, null)!;
        return new AgentExecutionRuntime([agent], new TestChatClientResolver(), new AgentToolInvoker(services), new EmptyRetriever(),
            new RagObservabilityProjection(Options.Create(new RagObservabilityOptions()), null, null, NullLogger<RagObservabilityProjection>.Instance),
            new TestRegistry(registration), new TestIngestionManager(status));
    }

    private sealed class TestRegistry(RagIndexRegistration registration) : IRagIndexRegistry
    {
        public IReadOnlyList<RagIndexRegistration> Registrations { get; } = [registration];
        public IReadOnlyList<RagIndexMetadata> GetMetadata() => [];
    }

    private sealed class TestIngestionManager(RagIndexRuntimeStatus status) : IRagIngestionManager
    {
        public Task<RagIngestionOperation> StartAsync(string indexName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public RagIndexRuntimeStatus GetStatus(string indexName) => status;
        public Task CancelAsync(string indexName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingRetriever : IRagRetriever
    {
        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<RagSearchResult>>(
                new RagRetrievalExecutionException("provider-secret", RetrievalErrorCode.EmbeddingFailed));
    }

    private sealed class InvalidScoreRetriever : IRagRetriever
    {
        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RagSearchResult>>(
            [
                new RagSearchResult
                {
                    Chunk = new RagChunk { Id = "chunk-1", DocumentId = "document-1", Content = "content" },
                    RawScore = 2,
                    Metric = RagScoreMetrics.CosineSimilarity,
                    HigherIsBetter = true,
                },
            ]);
    }
}
