using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Tools;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Tools;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Agents.Tests.Agents.Integration;

/// <summary>
/// End-to-end integration tests for the agent runtime Vector Query Tool bridge. They wire the real RAG
/// dependency injection graph (<c>AddRuniqRag</c> with the in-memory vector store and a deterministic embedding),
/// seed records into the in-memory store, resolve the production <c>IVectorQueryTool</c>, and drive
/// <see cref="AgentExecutionRuntime"/> with an agent configured through <c>UseVectorQueryTool</c>. This proves the
/// agent → tool → retrieval pipeline → in-memory store path works without any real provider, network, or database.
/// </summary>
public sealed class AgentVectorQueryToolIntegrationTests
{
    private const string StoreName = "in-memory-store";
    private const string IndexName = "documents";
    private const int Dimensions = 1;

    [Fact]
    public async Task ExecuteStreamAsync_ShouldRetrieveSeededResultsThroughRealVectorQueryTool()
    {
        using var provider = BuildRagProvider();
        await SeedRecordAsync(
            provider,
            IndexName,
            id: "chunk-1",
            content: "Bursa has notable regional food stops.",
            metadata: new Dictionary<string, string> { ["documentId"] = "doc-1" });

        var tool = provider.GetRequiredService<IVectorQueryTool>();
        var runtime = CreateRuntime(
            new Agent(
                    id: "rag-agent",
                    name: "RAG Agent",
                    instructions: "Answer travel questions.",
                    model: "ollama/llama3")
                .UseVectorQueryTool(StoreName, IndexName),
            tool);

        // The resolved production tool retrieves the seeded record through the real in-memory pipeline.
        var toolResult = await tool.ExecuteAsync(new VectorQueryToolRequest
        {
            VectorStoreName = StoreName,
            IndexName = IndexName,
            QueryText = "Bursa food stops",
        });
        Assert.True(toolResult.Succeeded);
        Assert.Contains(toolResult.Matches, match => match.RecordId == "chunk-1");

        var events = await DrainAsync(runtime.ExecuteStreamAsync("rag-agent", "Plan Bursa food stops."));

        // The agent runtime invokes the same tool over the real pipeline and completes without a RAG failure.
        Assert.DoesNotContain(events, item => item.Kind == AgentExecutionEventKind.Failed);
        Assert.Contains(events, item => item.Kind == AgentExecutionEventKind.Completed);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldCompleteWithEmptyContext_WhenIndexHasNoMatches()
    {
        using var provider = BuildRagProvider();
        await CreateIndexAsync(provider, IndexName);

        var tool = provider.GetRequiredService<IVectorQueryTool>();
        var runtime = CreateRuntime(
            new Agent(
                    id: "rag-agent",
                    name: "RAG Agent",
                    instructions: "Answer travel questions.",
                    model: "ollama/llama3")
                .UseVectorQueryTool(StoreName, IndexName),
            tool);

        var events = await DrainAsync(runtime.ExecuteStreamAsync("rag-agent", "Plan Bursa food stops."));

        // An empty (but created) index is a successful, empty retrieval, so the agent still completes.
        Assert.DoesNotContain(events, item => item.Kind == AgentExecutionEventKind.Failed);
        Assert.Contains(events, item => item.Kind == AgentExecutionEventKind.Completed);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldSurfaceRagRetrievalFailed_WhenConfiguredIndexWasNeverCreated()
    {
        using var provider = BuildRagProvider();

        var tool = provider.GetRequiredService<IVectorQueryTool>();
        var runtime = CreateRuntime(
            new Agent(
                    id: "rag-agent",
                    name: "RAG Agent",
                    instructions: "Answer travel questions.",
                    model: "ollama/llama3")
                .UseVectorQueryTool(StoreName, "missing-index"),
            tool);

        var events = await DrainAsync(runtime.ExecuteStreamAsync("rag-agent", "Plan Bursa food stops."));

        // A never-created index fails deterministically through the real pipeline and surfaces as RagRetrievalFailed.
        var failedEvent = Assert.Single(events, item => item.Kind == AgentExecutionEventKind.Failed);
        Assert.Equal("RagRetrievalFailed", failedEvent.ErrorCode);
        Assert.DoesNotContain(events, item => item.Kind == AgentExecutionEventKind.Completed);
    }

    private static ServiceProvider BuildRagProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRagEmbeddingProvider>(new ConstantEmbeddingProvider());
        services.AddRuniqRag(builder => builder.UseInMemoryVectorStore());

        return services.BuildServiceProvider();
    }

    private static async Task CreateIndexAsync(ServiceProvider provider, string indexName)
    {
        var vectorStore = provider.GetRequiredService<IRagVectorStore>();

        var result = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = indexName,
            Dimensions = Dimensions,
        });

        Assert.True(result.Succeeded);
    }

    private static async Task SeedRecordAsync(
        ServiceProvider provider,
        string indexName,
        string id,
        string content,
        IReadOnlyDictionary<string, string> metadata)
    {
        await CreateIndexAsync(provider, indexName);

        var vectorStore = provider.GetRequiredService<IRagVectorStore>();

        var upsertResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = indexName,
            Records =
            [
                new VectorRecord
                {
                    Id = id,
                    Values = [1f],
                    Content = content,
                    Metadata = new RagMetadata(metadata.ToDictionary(pair => pair.Key, pair => pair.Value)),
                },
            ],
        });

        Assert.True(upsertResult.Succeeded);
    }

    private static AgentExecutionRuntime CreateRuntime(Agent agent, IVectorQueryTool tool)
    {
        return new AgentExecutionRuntime(
            agents: [agent],
            openAIResponsesClient: new OpenAIResponsesClient(new HttpClient()),
            openAICompatibleClient: new OpenAICompatibleClient(new HttpClient()),
            toolInvoker: new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()),
            ragRetriever: null,
            vectorQueryTool: tool);
    }

    private static async Task<List<AgentExecutionEvent>> DrainAsync(
        IAsyncEnumerable<AgentExecutionEvent> events)
    {
        var collectedEvents = new List<AgentExecutionEvent>();

        await foreach (var executionEvent in events)
        {
            collectedEvents.Add(executionEvent);
        }

        return collectedEvents;
    }

    /// <summary>
    /// A minimal deterministic embedding test double that maps every text to the same single-dimension vector.
    /// It keeps the integration focused on the agent → tool → store wiring rather than similarity ranking, which
    /// the RAG-level Vector Query Tool integration tests already prove with keyword overlap.
    /// </summary>
    private sealed class ConstantEmbeddingProvider : IRagEmbeddingProvider
    {
        public Task<RagEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new RagEmbedding([1f]));
        }
    }
}

