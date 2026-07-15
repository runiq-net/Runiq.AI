using System.Text.Json;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Rag.Abstractions.Tools;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Tools;

namespace Runiq.AI.Agents.Tests.Tools;

/// <summary>
/// Unit tests for the agent-facing <see cref="VectorQueryTool"/> adapter. They use a fake
/// <see cref="IVectorQueryTool"/> to prove input→request mapping, result→output mapping, failure propagation,
/// adapter-boundary handling, and cancellation forwarding, without touching a real retrieval pipeline.
/// </summary>
public sealed class VectorQueryToolTests
{
    // Verifies that every input field, including EmbeddingModel, TopK, and a supplied metadata filter, is mapped
    // onto the delegated VectorQueryToolRequest unchanged.
    [Fact]
    public async Task ExecuteAsync_ShouldMapInputToRequest_WhenInputIsSupplied()
    {
        var fake = new FakeVectorQueryTool();
        var adapter = new VectorQueryTool(fake);
        var filter = new VectorQueryToolMetadataFilterInput
        {
            Criteria = [new VectorQueryToolMetadataCriterionInput { Key = "documentId", Value = "doc-1" }],
        };

        await adapter.ExecuteAsync(new VectorQueryToolInput
        {
            VectorStoreName = "store",
            IndexName = "documents",
            QueryText = "Bursa food stops",
            EmbeddingModel = "text-embedding-3-small",
            TopK = 7,
            MetadataFilter = filter,
        });

        Assert.NotNull(fake.CapturedRequest);
        var request = fake.CapturedRequest;
        Assert.Equal("store", request.VectorStoreName);
        Assert.Equal("documents", request.IndexName);
        Assert.Equal("Bursa food stops", request.QueryText);
        Assert.Equal("text-embedding-3-small", request.EmbeddingModel);
        Assert.Equal(7, request.TopK);
        var criterion = Assert.Single(request.MetadataFilter.Criteria);
        Assert.Equal("documentId", criterion.Key);
        Assert.Equal("doc-1", criterion.Value);
        Assert.Equal(RetrievalMetadataFilterOperator.Equal, criterion.Operator);
    }

    // Regression guard for review finding P2-1: exercises the real System.Text.Json (Web defaults) path that
    // AgentToolInvoker uses to bind tool input JSON, proving a model-supplied metadata filter survives
    // deserialization and reaches the delegated request with its criteria intact (the old design reusing
    // RetrievalMetadataTypes directly silently dropped these criteria).
    [Fact]
    public async Task ExecuteAsync_ShouldForwardDeserializedMetadataFilter_WhenSuppliedAsJson()
    {
        const string argumentsJson = """
        {
          "vectorStoreName": "store",
          "indexName": "documents",
          "queryText": "Bursa food stops",
          "topK": 3,
          "metadataFilter": {
            "criteria": [
              { "key": "documentId", "value": "doc-1" },
              { "key": "language", "value": "tr", "operator": 0 }
            ]
          }
        }
        """;
        var input = DeserializeInput(argumentsJson);
        var fake = new FakeVectorQueryTool();
        var adapter = new VectorQueryTool(fake);

        await adapter.ExecuteAsync(input);

        Assert.NotNull(fake.CapturedRequest);
        Assert.Equal(2, fake.CapturedRequest.MetadataFilter.Criteria.Count);
        Assert.Collection(
            fake.CapturedRequest.MetadataFilter.Criteria,
            first =>
            {
                Assert.Equal("documentId", first.Key);
                Assert.Equal("doc-1", first.Value);
                Assert.Equal(RetrievalMetadataFilterOperator.Equal, first.Operator);
            },
            second =>
            {
                Assert.Equal("language", second.Key);
                Assert.Equal("tr", second.Value);
                Assert.Equal(RetrievalMetadataFilterOperator.Equal, second.Operator);
            });
    }

    // Verifies that when the deserialized input carries no metadata filter (the JSON omits the field), the adapter
    // still maps to RetrievalMetadataFilter.Empty through the same runtime deserialization path.
    [Fact]
    public async Task ExecuteAsync_ShouldMapToEmptyFilter_WhenJsonOmitsMetadataFilter()
    {
        const string argumentsJson = """
        {
          "vectorStoreName": "store",
          "indexName": "documents",
          "queryText": "query"
        }
        """;
        var input = DeserializeInput(argumentsJson);
        var fake = new FakeVectorQueryTool();
        var adapter = new VectorQueryTool(fake);

        await adapter.ExecuteAsync(input);

        Assert.NotNull(fake.CapturedRequest);
        Assert.Same(RetrievalMetadataFilter.Empty, fake.CapturedRequest.MetadataFilter);
    }

    // Verifies that a malformed criterion supplied via JSON (a blank key that the RAG criterion contract rejects)
    // is surfaced as a deterministic InvalidRequest failed output at the adapter boundary, not thrown, and the
    // delegated tool is never invoked.
    [Fact]
    public async Task ExecuteAsync_ShouldReturnInvalidRequest_WhenJsonFilterCriterionIsMalformed()
    {
        const string argumentsJson = """
        {
          "vectorStoreName": "store",
          "indexName": "documents",
          "queryText": "query",
          "metadataFilter": { "criteria": [ { "key": " ", "value": "x" } ] }
        }
        """;
        var input = DeserializeInput(argumentsJson);
        var fake = new FakeVectorQueryTool();
        var adapter = new VectorQueryTool(fake);

        var output = await adapter.ExecuteAsync(input);

        Assert.False(output.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, output.ErrorCode);
        Assert.Null(fake.CapturedRequest);
    }

    // Regression guard for review finding P2-1: an explicit "criteria": null (which System.Text.Json binds as
    // null over the default) must map deterministically to RetrievalMetadataFilter.Empty, not throw a
    // NullReferenceException out of the adapter.
    [Fact]
    public async Task ExecuteAsync_ShouldMapToEmptyFilter_WhenJsonCriteriaIsExplicitlyNull()
    {
        const string argumentsJson = """
        {
          "vectorStoreName": "store",
          "indexName": "documents",
          "queryText": "query",
          "metadataFilter": { "criteria": null }
        }
        """;
        var input = DeserializeInput(argumentsJson);
        var fake = new FakeVectorQueryTool();
        var adapter = new VectorQueryTool(fake);

        var output = await adapter.ExecuteAsync(input);

        Assert.True(output.Succeeded);
        Assert.NotNull(fake.CapturedRequest);
        Assert.Same(RetrievalMetadataFilter.Empty, fake.CapturedRequest.MetadataFilter);
    }

    // Regression guard for review finding P3-1: the operator must deserialize from its string name ("Equal"),
    // not only numerically, so a name-based value is not rejected by the runtime's numeric-only Web defaults.
    [Fact]
    public async Task ExecuteAsync_ShouldForwardOperator_WhenSuppliedAsStringName()
    {
        const string argumentsJson = """
        {
          "vectorStoreName": "store",
          "indexName": "documents",
          "queryText": "query",
          "metadataFilter": { "criteria": [ { "key": "language", "value": "tr", "operator": "Equal" } ] }
        }
        """;
        var input = DeserializeInput(argumentsJson);
        var fake = new FakeVectorQueryTool();
        var adapter = new VectorQueryTool(fake);

        await adapter.ExecuteAsync(input);

        Assert.NotNull(fake.CapturedRequest);
        var criterion = Assert.Single(fake.CapturedRequest.MetadataFilter.Criteria);
        Assert.Equal("language", criterion.Key);
        Assert.Equal(RetrievalMetadataFilterOperator.Equal, criterion.Operator);
    }

    // Regression guard for review finding P3-2: the agent-facing failure reason for a malformed criterion must
    // not leak the internal "(Parameter '...')" suffix that ArgumentException.Message appends.
    [Fact]
    public async Task ExecuteAsync_ShouldNotLeakParameterName_WhenCriterionIsMalformed()
    {
        const string argumentsJson = """
        {
          "vectorStoreName": "store",
          "indexName": "documents",
          "queryText": "query",
          "metadataFilter": { "criteria": [ { "key": " ", "value": "x" } ] }
        }
        """;
        var input = DeserializeInput(argumentsJson);
        var fake = new FakeVectorQueryTool();
        var adapter = new VectorQueryTool(fake);

        var output = await adapter.ExecuteAsync(input);

        Assert.False(output.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, output.ErrorCode);
        Assert.DoesNotContain("Parameter", output.Reason, StringComparison.Ordinal);
        Assert.NotEmpty(output.Reason);
    }

    // Verifies that an absent metadata filter maps to RetrievalMetadataFilter.Empty so retrieval always receives
    // a non-null filter that applies no constraints.
    [Fact]
    public async Task ExecuteAsync_ShouldMapAbsentFilterToEmpty_WhenNoFilterIsSupplied()
    {
        var fake = new FakeVectorQueryTool();
        var adapter = new VectorQueryTool(fake);

        await adapter.ExecuteAsync(new VectorQueryToolInput
        {
            VectorStoreName = "store",
            IndexName = "documents",
            QueryText = "query",
        });

        Assert.NotNull(fake.CapturedRequest);
        Assert.Same(RetrievalMetadataFilter.Empty, fake.CapturedRequest.MetadataFilter);
    }

    // Verifies that the input default TopK matches the request default of five when the agent does not supply one.
    [Fact]
    public async Task ExecuteAsync_ShouldForwardDefaultTopK_WhenTopKIsNotSupplied()
    {
        var fake = new FakeVectorQueryTool();
        var adapter = new VectorQueryTool(fake);

        await adapter.ExecuteAsync(new VectorQueryToolInput
        {
            VectorStoreName = "store",
            IndexName = "documents",
            QueryText = "query",
        });

        Assert.NotNull(fake.CapturedRequest);
        Assert.Equal(5, fake.CapturedRequest.TopK);
    }

    // Verifies that a successful delegated result with matches maps to a successful output preserving matches and metadata.
    [Fact]
    public async Task ExecuteAsync_ShouldMapSuccessResult_WhenDelegatedToolSucceeds()
    {
        var match = new RetrievalResultItem
        {
            RecordId = "chunk-1",
            Content = "content",
            Score = 0.9,
        };
        var metadata = new RagMetadata(new Dictionary<string, string> { ["k"] = "v" });
        var fake = new FakeVectorQueryTool
        {
            Result = VectorQueryToolResult.Success([match], metadata),
        };
        var adapter = new VectorQueryTool(fake);

        var output = await adapter.ExecuteAsync(ValidInput());

        Assert.True(output.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, output.ErrorCode);
        Assert.Equal(string.Empty, output.Reason);
        var returnedMatch = Assert.Single(output.Matches);
        Assert.Equal("chunk-1", returnedMatch.RecordId);
        Assert.Same(metadata, output.Metadata);
    }

    // Verifies that a successful-but-empty delegated result maps to a successful output with no matches (an empty
    // result is never a failure).
    [Fact]
    public async Task ExecuteAsync_ShouldMapEmptySuccessResult_WhenDelegatedToolMatchesNothing()
    {
        var fake = new FakeVectorQueryTool
        {
            Result = VectorQueryToolResult.Success(),
        };
        var adapter = new VectorQueryTool(fake);

        var output = await adapter.ExecuteAsync(ValidInput());

        Assert.True(output.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, output.ErrorCode);
        Assert.Empty(output.Matches);
    }

    // Verifies that a failed delegated result is propagated as a failed output carrying the same error code and
    // reason, rather than being thrown.
    [Fact]
    public async Task ExecuteAsync_ShouldPropagateFailureResult_WhenDelegatedToolFails()
    {
        var fake = new FakeVectorQueryTool
        {
            Result = VectorQueryToolResult.Failure(RetrievalErrorCode.RetrievalFailed, "index unavailable"),
        };
        var adapter = new VectorQueryTool(fake);

        var output = await adapter.ExecuteAsync(ValidInput());

        Assert.False(output.Succeeded);
        Assert.Equal(RetrievalErrorCode.RetrievalFailed, output.ErrorCode);
        Assert.Equal("index unavailable", output.Reason);
        Assert.Empty(output.Matches);
    }

    // Verifies the adapter-boundary condition the delegated tool cannot see: a null input from the runtime yields
    // a deterministic InvalidRequest failure without invoking the delegated tool.
    [Fact]
    public async Task ExecuteAsync_ShouldReturnInvalidRequest_WhenInputIsNull()
    {
        var fake = new FakeVectorQueryTool();
        var adapter = new VectorQueryTool(fake);

        var output = await adapter.ExecuteAsync(null!);

        Assert.False(output.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, output.ErrorCode);
        Assert.Null(fake.CapturedRequest);
    }

    // Verifies that the CancellationToken is forwarded unchanged through the delegation.
    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken_WhenTokenIsSupplied()
    {
        var fake = new FakeVectorQueryTool();
        var adapter = new VectorQueryTool(fake);
        using var cts = new CancellationTokenSource();

        await adapter.ExecuteAsync(ValidInput(), cts.Token);

        Assert.Equal(cts.Token, fake.CapturedToken);
    }

    // Verifies that cancellation observed by the delegated tool propagates out of the adapter, matching the
    // existing cancellation contract (cancellation is thrown, not swallowed into a failed output).
    [Fact]
    public async Task ExecuteAsync_ShouldPropagateCancellation_WhenDelegatedToolCancels()
    {
        var fake = new FakeVectorQueryTool
        {
            Handler = (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(VectorQueryToolResult.Success());
            },
        };
        var adapter = new VectorQueryTool(fake);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => adapter.ExecuteAsync(ValidInput(), cts.Token));
    }

    private static VectorQueryToolInput ValidInput()
    {
        return new VectorQueryToolInput
        {
            VectorStoreName = "store",
            IndexName = "documents",
            QueryText = "query",
        };
    }

    // Deserializes tool input using the same System.Text.Json Web-default options AgentToolInvoker binds tool
    // arguments with, so these tests exercise the real runtime deserialization path rather than in-process
    // C# object construction.
    private static VectorQueryToolInput DeserializeInput(string argumentsJson)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var input = JsonSerializer.Deserialize<VectorQueryToolInput>(argumentsJson, options);
        Assert.NotNull(input);
        return input;
    }

    /// <summary>
    /// A deterministic <see cref="IVectorQueryTool"/> test double that captures the delegated request and token
    /// and returns a configured result (or runs a custom handler), with no real retrieval pipeline.
    /// </summary>
    private sealed class FakeVectorQueryTool : IVectorQueryTool
    {
        public VectorQueryToolRequest? CapturedRequest { get; private set; }

        public CancellationToken CapturedToken { get; private set; }

        public VectorQueryToolResult Result { get; init; } = VectorQueryToolResult.Success();

        public Func<VectorQueryToolRequest, CancellationToken, Task<VectorQueryToolResult>>? Handler { get; init; }

        public Task<VectorQueryToolResult> ExecuteAsync(
            VectorQueryToolRequest request,
            CancellationToken cancellationToken = default)
        {
            CapturedRequest = request;
            CapturedToken = cancellationToken;

            return Handler is not null
                ? Handler(request, cancellationToken)
                : Task.FromResult(Result);
        }
    }
}

