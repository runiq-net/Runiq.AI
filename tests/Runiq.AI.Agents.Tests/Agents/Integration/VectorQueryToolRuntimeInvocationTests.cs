using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;

namespace Runiq.AI.Agents.Tests.Agents.Integration;

/// <summary>
/// End-to-end integration tests proving the EU-001 <see cref="VectorQueryTool"/> adapter participates in the
/// existing agent tool runtime path with no new registry or bridge: it is described by
/// <see cref="AgentToolRegistration.FromToolType"/>, attaches to an <see cref="Agent"/> via
/// <see cref="AgentToolExtensions.AddTool{TTool}"/>, and runs through <see cref="AgentToolInvoker"/> against a
/// service provider configured with the real RAG DI (<c>AddRuniqRag</c>) plus a deterministic fake retrieval
/// pipeline. Assertions target the JSON output shape the invoker produces (System.Text.Json Web defaults), not
/// internal types, and confirm topK and metadata filter are honored through the delegation. No real network,
/// provider, or database is used.
/// </summary>
public sealed class VectorQueryToolRuntimeInvocationTests
{
    private const string ToolName = "vector_query";

    // Verifies the adapter is discoverable through the existing registration path: the tool name, description,
    // and the input/output CLR types are derived from the [RuniqTool] attribute and the IRuniqTool<,> interface.
    [Fact]
    public void FromToolType_ShouldDescribeVectorQueryToolAdapter()
    {
        var registration = AgentToolRegistration.FromToolType(typeof(VectorQueryTool));

        Assert.Equal(ToolName, registration.Name);
        Assert.False(string.IsNullOrWhiteSpace(registration.Description));
        Assert.Equal(typeof(VectorQueryTool), registration.ToolType);
        Assert.Equal(typeof(VectorQueryToolInput), registration.InputType);
        Assert.Equal(typeof(VectorQueryToolOutput), registration.OutputType);
    }

    // Verifies the adapter attaches to an agent via the existing AddTool<T>() API with no new discovery mechanism,
    // exposing the tool under its model-facing name in Agent.Tools.
    [Fact]
    public void AddTool_ShouldAttachVectorQueryToolToAgent()
    {
        var agent = CreateAgent().AddTool<VectorQueryTool>();

        Assert.Contains(agent.Tools, tool => tool.Name == ToolName);
    }

    // Verifies the full runtime path: AgentToolInvoker deserializes the model's JSON arguments, resolves the
    // adapter (and the real DefaultVectorQueryTool) from AddRuniqRag, delegates to the fake pipeline, and returns
    // agent-usable success JSON carrying the mapped matches. Also proves topK and the metadata filter are honored
    // end-to-end through the delegation.
    [Fact]
    public async Task InvokeAsync_ShouldReturnSuccessJsonWithMatches_AndForwardTopKAndFilter()
    {
        var pipeline = new FakeRetrievalPipeline
        {
            Result = RetrievalResult.Success(
                [new RetrievalResultItem { RecordId = "chunk-1", Content = "Bursa food stops", RawScore = 0.87 }],
                RagMetadata.Empty),
        };
        using var provider = BuildProvider(pipeline);
        var invoker = new AgentToolInvoker(provider);
        var agent = CreateAgent().AddTool<VectorQueryTool>();

        const string argumentsJson = """
        {
          "vectorStoreName": "in-memory-store",
          "indexName": "documents",
          "queryText": "Bursa food stops",
          "topK": 3,
          "metadataFilter": { "criteria": [ { "key": "documentId", "value": "doc-1" } ] }
        }
        """;

        var result = await invoker.InvokeAsync(agent, ToolName, argumentsJson);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputJson);

        using var document = JsonDocument.Parse(result.OutputJson!);
        var root = document.RootElement;
        Assert.True(root.GetProperty("succeeded").GetBoolean());
        var match = Assert.Single(root.GetProperty("matches").EnumerateArray());
        Assert.Equal("chunk-1", match.GetProperty("recordId").GetString());
        Assert.Equal("Bursa food stops", match.GetProperty("content").GetString());

        // topK and the metadata filter flowed adapter → DefaultVectorQueryTool → retrieval pipeline unchanged.
        Assert.NotNull(pipeline.CapturedRequest);
        Assert.Equal(3, pipeline.CapturedRequest!.TopK);
        var criterion = Assert.Single(pipeline.CapturedRequest.MetadataFilter.Criteria);
        Assert.Equal("documentId", criterion.Key);
        Assert.Equal("doc-1", criterion.Value);
    }

    // Verifies the failure path: a deterministic pipeline failure is mapped to agent-usable JSON with the
    // provider-independent error code, and the invocation itself still succeeds (the tool ran and produced output).
    [Fact]
    public async Task InvokeAsync_ShouldReturnFailureJsonWithErrorCode_WhenPipelineFails()
    {
        var pipeline = new FakeRetrievalPipeline
        {
            Result = RetrievalResult.Failure(RetrievalErrorCode.RetrievalFailed, "index unavailable"),
        };
        using var provider = BuildProvider(pipeline);
        var invoker = new AgentToolInvoker(provider);
        var agent = CreateAgent().AddTool<VectorQueryTool>();

        const string argumentsJson = """
        {
          "vectorStoreName": "in-memory-store",
          "indexName": "documents",
          "queryText": "Bursa food stops"
        }
        """;

        var result = await invoker.InvokeAsync(agent, ToolName, argumentsJson);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputJson);

        using var document = JsonDocument.Parse(result.OutputJson!);
        var root = document.RootElement;
        Assert.False(root.GetProperty("succeeded").GetBoolean());
        Assert.Equal((int)RetrievalErrorCode.RetrievalFailed, root.GetProperty("errorCode").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("reason").GetString()));
        Assert.Empty(root.GetProperty("matches").EnumerateArray());
    }

    private static Agent CreateAgent()
    {
        return new Agent(
            id: "rag-agent",
            name: "RAG Agent",
            instructions: "Answer travel questions.",
            model: "ollama/llama3");
    }

    // Builds a service provider from the real RAG DI graph but substitutes a deterministic fake retrieval
    // pipeline. The fake is registered before AddRuniqRag so its TryAddScoped registration does not override it,
    // leaving the production DefaultVectorQueryTool resolving against the fake.
    private static ServiceProvider BuildProvider(IRagRetrievalPipeline pipeline)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => pipeline);
        services.AddRuniqRag();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A deterministic <see cref="IRagRetrievalPipeline"/> test double that captures the delegated retrieval
    /// request (to assert topK and metadata-filter forwarding) and returns a configured result, with no real
    /// embedding, vector store, network, or database.
    /// </summary>
    private sealed class FakeRetrievalPipeline : IRagRetrievalPipeline
    {
        public RetrievalRequest? CapturedRequest { get; private set; }

        public RetrievalResult Result { get; init; } = RetrievalResult.Success();

        public Task<RetrievalResult> RetrieveAsync(
            RetrievalRequest request,
            CancellationToken cancellationToken = default)
        {
            CapturedRequest = request;

            return Task.FromResult(Result);
        }
    }
}

