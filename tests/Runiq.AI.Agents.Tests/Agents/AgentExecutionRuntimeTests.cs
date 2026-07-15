using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Runiq.AI.Agents.Providers.OpenAI;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Agents.Tests.TestDoubles;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Core.AI.Capabilities;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Configuration;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Tools;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.Tools;
using Runiq.AI.Rag.Models.VectorStores;
using Runiq.AI.Rag.Retrieval;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class AgentExecutionRuntimeTests
{
    // Verifies that named provider-model configuration is projected into the request before the chat client resolves it.
    [Fact]
    public async Task ExecuteStreamAsync_ShouldProjectNamedModelCapabilitiesBeforeClientResolution()
    {
        var provider = new ProviderOptions
        {
            Models = new Dictionary<string, ProviderModelOptions>
            {
                ["chat"] = new() { Model = "private-qwen", Capabilities = [ModelCapability.Chat, ModelCapability.Streaming] }
            }
        };
        var agent = new Agent("configured-agent", "Configured", "Help.", "ollama/chat", provider: provider);
        var resolver = new TestChatClientResolver();
        var runtime = new AgentExecutionRuntime(
            [agent],
            resolver,
            new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()));

        await DrainAsync(runtime.ExecuteStreamAsync(agent.Id, "hello"));

        Assert.Single(resolver.Requests);
        Assert.Equal(ModelCapability.Chat | ModelCapability.Streaming, resolver.Requests[0].Model.Capabilities);
        Assert.Equal("private-qwen", resolver.Requests[0].Model.ModelName);
    }

    // The runtime constructor must depend on the shared Core resolver instead of concrete provider clients.
    [Fact]
    public void Constructor_ShouldExposeProviderNeutralResolverOverload()
    {
        var constructor = typeof(AgentExecutionRuntime).GetConstructor([
            typeof(IEnumerable<Agent>),
            typeof(IChatClientResolver),
            typeof(AgentToolInvoker),
            typeof(IRagRetriever),
            typeof(IVectorQueryTool)
        ]);

        Assert.NotNull(constructor);
    }

    // The provider-neutral constructor accepts an optional retriever without adding a provider-specific overload.
    [Fact]
    public void Constructor_ShouldExposeRagRetrieverOverload()
    {
        var constructor = typeof(AgentExecutionRuntime).GetConstructor([
            typeof(IEnumerable<Agent>),
            typeof(IChatClientResolver),
            typeof(AgentToolInvoker),
            typeof(IRagRetriever),
            typeof(IVectorQueryTool)
        ]);

        Assert.NotNull(constructor);

        var retriever = new TrackingRagRetriever([]);
        var runtime = new AgentExecutionRuntime(
            agents: [],
            chatClientResolver: new TestChatClientResolver(),
            toolInvoker: new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()),
            ragRetriever: retriever);

        Assert.NotNull(runtime);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldForwardAgentRagIndexNameToRetriever()
    {
        var agent = CreateRagAgent().UseRagIndex("documents");
        var retriever = new TrackingRagRetriever([]);
        var runtime = CreateRuntimeWithRag(agent, retriever);

        await DrainAsync(runtime.ExecuteStreamAsync(agent.Id, "Find travel notes."));

        Assert.NotNull(retriever.Query);
        Assert.Equal("documents", retriever.Query.IndexName);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldUseRuntimeIndexName_WhenRuntimeIndexOverridesAgentRagIndex()
    {
        var agent = CreateRagAgent().UseRagIndex("documents");
        var retriever = new TrackingRagRetriever([]);
        var runtime = CreateRuntimeWithRag(agent, retriever);

        await DrainAsync(runtime.ExecuteStreamAsync(
            agent.Id,
            new AgentQuery("Find travel notes.") { IndexName = "archive" }));

        Assert.Equal("archive", retriever.Query!.IndexName);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldLetRetrievalDefaultIndexNameResolve_WhenAgentRagIndexNameIsMissing()
    {
        var agent = new Agent(
            id: "travel-agent",
            name: "Travel Agent",
            instructions: "Plan short travel routes.",
            model: "ollama/llama3",
            rag: new());
        var vectorStore = new SearchOnlyRagVectorStore([]);
        var retriever = new DefaultRetriever(
            new StaticEmbeddingClient([1.0f]),
            vectorStore,
            Options.Create(new RagOptions { DefaultIndexName = "default-index" }));
        var runtime = CreateRuntimeWithRag(agent, retriever);

        await DrainAsync(runtime.ExecuteStreamAsync(agent.Id, "Find travel notes."));

        Assert.True(vectorStore.SearchAsyncWasCalled);
        Assert.Equal("default-index", vectorStore.SearchQuery!.IndexName);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldFailDeterministically_WhenRetrieverRejectsMissingIndexName()
    {
        var agent = new Agent(
            id: "travel-agent",
            name: "Travel Agent",
            instructions: "Plan short travel routes.",
            model: "ollama/llama3",
            rag: new());
        var retriever = new DefaultRetriever(
            new StaticEmbeddingClient([1.0f]),
            new SearchOnlyRagVectorStore([]));
        var runtime = CreateRuntimeWithRag(
            agent,
            retriever);

        var events = await DrainAsync(runtime.ExecuteStreamAsync(agent.Id, "Find travel notes."));

        var failedEvent = Assert.Single(events, item => item.Kind == AgentExecutionEventKind.Failed);
        Assert.Equal("RagRetrievalFailed", failedEvent.ErrorCode);
        Assert.Contains("vector index name", failedEvent.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldEmitFailure_WhenRagRetrieverFails()
    {
        var agent = CreateRagAgent().UseRagIndex("documents");
        var runtime = CreateRuntimeWithRag(
            agent,
            new FailingRagRetriever("Vector index has not been created."));

        var events = await DrainAsync(runtime.ExecuteStreamAsync(agent.Id, "Find travel notes."));

        var failedEvent = Assert.Single(events, item => item.Kind == AgentExecutionEventKind.Failed);
        Assert.Equal("RagRetrievalFailed", failedEvent.ErrorCode);
        Assert.Equal("Vector index has not been created.", failedEvent.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldUseRuntimeIndexNameWithRealRetriever_WhenRuntimeIndexOverridesAgentRagIndex()
    {
        var agent = CreateRagAgent().UseRagIndex("documents");
        var vectorStore = new SearchOnlyRagVectorStore([]);
        var retriever = new DefaultRetriever(
            new StaticEmbeddingClient([1.0f]),
            vectorStore,
            Options.Create(new RagOptions { DefaultIndexName = "default-index" }));
        var runtime = CreateRuntimeWithRag(agent, retriever);

        await DrainAsync(runtime.ExecuteStreamAsync(
            agent.Id,
            new AgentQuery("Find travel notes.") { IndexName = "archive" }));

        Assert.True(vectorStore.SearchAsyncWasCalled);
        Assert.Equal("archive", vectorStore.SearchQuery!.IndexName);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldSupportDifferentAgentRagIndexNames()
    {
        var documentsAgent = CreateRagAgent("documents-agent").UseRagIndex("documents");
        var archiveAgent = CreateRagAgent("archive-agent").UseRagIndex("archive");
        var retriever = new TrackingRagRetriever([]);
        var runtime = new AgentExecutionRuntime(
            agents: [documentsAgent, archiveAgent],
            chatClientResolver: new TestChatClientResolver(),
            toolInvoker: new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()),
            ragRetriever: retriever);

        await DrainAsync(runtime.ExecuteStreamAsync(documentsAgent.Id, "Find documents."));
        Assert.Equal("documents", retriever.Query!.IndexName);

        await DrainAsync(runtime.ExecuteStreamAsync(archiveAgent.Id, "Find archive."));
        Assert.Equal("archive", retriever.Query!.IndexName);
    }

    // Verifies the runtime exposes a public constructor overload that accepts the Vector Query Tool.
    // The provider-neutral constructor accepts an optional vector query tool while tool execution remains Agent-owned.
    [Fact]
    public void Constructor_ShouldAcceptVectorQueryTool()
    {
        var constructor = typeof(AgentExecutionRuntime).GetConstructor([
            typeof(IEnumerable<Agent>),
            typeof(IChatClientResolver),
            typeof(AgentToolInvoker),
            typeof(IRagRetriever),
            typeof(IVectorQueryTool)
        ]);

        Assert.NotNull(constructor);

        var runtime = new AgentExecutionRuntime(
            agents: [],
            chatClientResolver: new TestChatClientResolver(),
            toolInvoker: new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()),
            ragRetriever: null,
            vectorQueryTool: new CapturingVectorQueryTool(VectorQueryToolResult.Success()));

        Assert.NotNull(runtime);
    }

    // Verifies the Vector Query Tool is invoked with the agent's associated store, index, embedding model, the
    // default top-k, and a non-null metadata filter when the agent configures a vector store name.
    [Fact]
    public async Task ExecuteStreamAsync_ShouldInvokeVectorQueryTool_WhenAgentConfiguresVectorStore()
    {
        var agent = CreateRagAgent().UseVectorQueryTool("primary-store", "documents", "text-embed");
        var tool = new CapturingVectorQueryTool(VectorQueryToolResult.Success());
        var runtime = CreateRuntimeWithVectorQueryTool(agent, tool);

        await DrainAsync(runtime.ExecuteStreamAsync(agent.Id, "Find travel notes."));

        Assert.Equal(1, tool.InvocationCount);
        Assert.NotNull(tool.Request);
        Assert.Equal("primary-store", tool.Request!.VectorStoreName);
        Assert.Equal("documents", tool.Request.IndexName);
        Assert.Equal("Find travel notes.", tool.Request.QueryText);
        Assert.Equal("text-embed", tool.Request.EmbeddingModel);
        Assert.Equal(5, tool.Request.TopK);
        Assert.Same(RetrievalMetadataFilter.Empty, tool.Request.MetadataFilter);
    }

    // Verifies that a runtime per-query index name overrides the agent's configured index when the tool runs.
    [Fact]
    public async Task ExecuteStreamAsync_ShouldForwardRuntimeIndexNameToVectorQueryTool_WhenOverridden()
    {
        var agent = CreateRagAgent().UseVectorQueryTool("primary-store", "documents");
        var tool = new CapturingVectorQueryTool(VectorQueryToolResult.Success());
        var runtime = CreateRuntimeWithVectorQueryTool(agent, tool);

        await DrainAsync(runtime.ExecuteStreamAsync(
            agent.Id,
            new AgentQuery("Find travel notes.") { IndexName = "archive" }));

        Assert.Equal("archive", tool.Request!.IndexName);
    }

    // Verifies the existing retriever path is used and the Vector Query Tool is not invoked when the agent uses
    // UseRagIndex, even if a Vector Query Tool is also available to the runtime.
    [Fact]
    public async Task ExecuteStreamAsync_ShouldUseRetrieverAndNotInvokeTool_WhenAgentUsesRagIndexPath()
    {
        var agent = CreateRagAgent().UseRagIndex("documents");
        var retriever = new TrackingRagRetriever([]);
        var tool = new CapturingVectorQueryTool(VectorQueryToolResult.Success());
        var runtime = new AgentExecutionRuntime(
            agents: [agent],
            chatClientResolver: new TestChatClientResolver(),
            toolInvoker: new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()),
            ragRetriever: retriever,
            vectorQueryTool: tool);

        await DrainAsync(runtime.ExecuteStreamAsync(agent.Id, "Find travel notes."));

        Assert.Equal("documents", retriever.Query!.IndexName);
        Assert.Equal(0, tool.InvocationCount);
    }

    // Verifies a failed Vector Query Tool result surfaces deterministically as a RagRetrievalFailed event, the
    // same failure contract used by the retriever path.
    [Fact]
    public async Task ExecuteStreamAsync_ShouldEmitFailure_WhenVectorQueryToolFails()
    {
        var agent = CreateRagAgent().UseVectorQueryTool("primary-store", "documents");
        var tool = new CapturingVectorQueryTool(
            VectorQueryToolResult.Failure(RetrievalErrorCode.InvalidRequest, "Vector index has not been created."));
        var runtime = CreateRuntimeWithVectorQueryTool(agent, tool);

        var events = await DrainAsync(runtime.ExecuteStreamAsync(agent.Id, "Find travel notes."));

        var failedEvent = Assert.Single(events, item => item.Kind == AgentExecutionEventKind.Failed);
        Assert.Equal("RagRetrievalFailed", failedEvent.ErrorCode);
        Assert.Equal("Vector index has not been created.", failedEvent.ErrorMessage);
    }

    // Verifies successful tool matches (including a match with an empty record id) map into the runtime RAG
    // context and are consumed by context assembly without error, letting execution complete.
    [Fact]
    public async Task ExecuteStreamAsync_ShouldCompleteWithMappedMatches_WhenVectorQueryToolReturnsResults()
    {
        var agent = CreateRagAgent().UseVectorQueryTool("primary-store", "documents");
        var tool = new CapturingVectorQueryTool(VectorQueryToolResult.Success(
        [
            new RetrievalResultItem
            {
                RecordId = "chunk-1",
                Content = "Bursa has notable regional food stops.",
                Score = 0.91,
                Metadata = new RagMetadata(new Dictionary<string, string> { ["documentId"] = "doc-1" }),
            },
            new RetrievalResultItem
            {
                RecordId = string.Empty,
                Content = "Fallback content without a record id.",
                Score = 0.42,
            },
        ]));
        var runtime = CreateRuntimeWithVectorQueryTool(agent, tool);

        var events = await DrainAsync(runtime.ExecuteStreamAsync(agent.Id, "Find travel notes."));

        Assert.DoesNotContain(events, item => item.Kind == AgentExecutionEventKind.Failed);
        Assert.Contains(events, item => item.Kind == AgentExecutionEventKind.Completed);
    }

    // Verifies the runtime forwards the caller's cancellation token through the Vector Query Tool call chain.
    [Fact]
    public async Task ExecuteStreamAsync_ShouldForwardCancellationTokenToVectorQueryTool()
    {
        var agent = CreateRagAgent().UseVectorQueryTool("primary-store", "documents");
        var tool = new CapturingVectorQueryTool(VectorQueryToolResult.Success());
        var runtime = CreateRuntimeWithVectorQueryTool(agent, tool);
        using var cancellationSource = new CancellationTokenSource();

        await DrainAsync(runtime.ExecuteStreamAsync(
            agent.Id,
            "Find travel notes.",
            cancellationToken: cancellationSource.Token));

        Assert.Equal(cancellationSource.Token, tool.CancellationToken);
    }

    // Verifies that only source search results above the confidence threshold are selected.
    // Verifies that no source results are selected when every score is below the minimum threshold.
    // Verifies that rejected source search results are not exposed as selected model context.
    private sealed class TrackingRagRetriever : IRagRetriever
    {
        private readonly IReadOnlyList<RagSearchResult> results;

        public TrackingRagRetriever(IReadOnlyList<RagSearchResult> results)
        {
            this.results = results;
        }

        public RagQuery? Query { get; private set; }

        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default)
        {
            Query = query;

            return Task.FromResult(results);
        }
    }

    private sealed class StaticEmbeddingClient : IEmbeddingClient
    {
        private readonly IReadOnlyList<float> vector;

        public StaticEmbeddingClient(IReadOnlyList<float> vector)
        {
            this.vector = vector;
        }

        public Task<EmbeddingResponse> EmbedAsync(
            EmbeddingRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new EmbeddingResponse(
                request.Inputs.Select((_, index) => new EmbeddingResult(index, vector, vector.Count)).ToList()));
        }
    }

    private sealed class FailingRagRetriever : IRagRetriever
    {
        private readonly string message;

        public FailingRagRetriever(string message)
        {
            this.message = message;
        }

        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturingVectorQueryTool : IVectorQueryTool
    {
        private readonly VectorQueryToolResult result;

        public CapturingVectorQueryTool(VectorQueryToolResult result)
        {
            this.result = result;
        }

        public VectorQueryToolRequest? Request { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public int InvocationCount { get; private set; }

        public Task<VectorQueryToolResult> ExecuteAsync(
            VectorQueryToolRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            CancellationToken = cancellationToken;
            InvocationCount++;

            return Task.FromResult(result);
        }
    }

    private sealed class SearchOnlyRagVectorStore : IRagVectorStore
    {
        private readonly IReadOnlyList<RagSearchResult> results;

        public SearchOnlyRagVectorStore(IReadOnlyList<RagSearchResult> results)
        {
            this.results = results;
        }

        public bool SearchAsyncWasCalled { get; private set; }

        public RagQuery? SearchQuery { get; private set; }

        public Task<CreateVectorIndexResult> CreateIndexAsync(
            CreateVectorIndexRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CreateVectorIndexResult
            {
                IndexName = request.IndexName,
                Succeeded = true,
            });
        }

        public Task<UpsertVectorResult> UpsertAsync(
            UpsertVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UpsertVectorResult
            {
                Succeeded = true,
                UpsertedCount = request.Records?.Count ?? 0,
            });
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            SearchAsyncWasCalled = true;
            SearchQuery = query;

            return Task.FromResult(results);
        }
    }

    private static Agent CreateRagAgent(string id = "travel-agent")
    {
        return new Agent(
            id: id,
            name: "Travel Agent",
            instructions: "Plan short travel routes.",
            model: "ollama/llama3");
    }

    private static AgentExecutionRuntime CreateRuntimeWithRag(
        Agent agent,
        IRagRetriever retriever)
    {
        return new AgentExecutionRuntime(
            agents: [agent],
            chatClientResolver: new TestChatClientResolver(),
            toolInvoker: new AgentToolInvoker(new ServiceCollection().BuildServiceProvider()),
            ragRetriever: retriever);
    }

    private static AgentExecutionRuntime CreateRuntimeWithVectorQueryTool(
        Agent agent,
        IVectorQueryTool tool)
    {
        return new AgentExecutionRuntime(
            agents: [agent],
            chatClientResolver: new TestChatClientResolver(),
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
}

