using Runiq.Agents.Tools;
using Runiq.Rag.Abstractions.Tools;
using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Retrieval;
using Runiq.Rag.Models.Tools;

namespace Runiq.Agents.Tests.Tools;

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
        var filter = new RetrievalMetadataFilter(
            [new RetrievalMetadataFilterCriterion("documentId", "doc-1")]);

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
        Assert.Same(filter, request.MetadataFilter);
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
