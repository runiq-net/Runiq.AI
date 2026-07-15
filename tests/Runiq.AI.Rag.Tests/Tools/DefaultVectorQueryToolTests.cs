using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Tools;
using Runiq.AI.Rag.Tools;

namespace Runiq.AI.Rag.Tests.Tools;

public sealed class DefaultVectorQueryToolTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenRetrievalPipelineIsNull()
    {
        // Verifies that the tool rejects a null retrieval pipeline as a programming error rather than deferring the failure.
        var exception = Assert.Throws<ArgumentNullException>(() => new DefaultVectorQueryTool(null!));

        Assert.Equal("retrievalPipeline", exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardIndexNameQueryTextTopKAndMetadataFilterToRetrievalPipeline()
    {
        // Verifies that the tool adapts the request onto the existing retrieval pipeline, forwarding index name, query text, top-k, and metadata filter unchanged.
        var pipeline = new TrackingRetrievalPipeline();
        var tool = new DefaultVectorQueryTool(pipeline);
        var metadataFilter = new RetrievalMetadataFilter(new Dictionary<string, string>
        {
            ["tenant"] = "runiq",
        });
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
            TopK = 9,
            MetadataFilter = metadataFilter,
        };

        await tool.ExecuteAsync(request);

        Assert.True(pipeline.RetrieveWasCalled);
        Assert.Equal("documents", pipeline.LastRequest?.IndexName);
        Assert.Equal("What is Runiq?", pipeline.LastRequest?.QueryText);
        Assert.Equal(9, pipeline.LastRequest?.TopK);
        Assert.Same(metadataFilter, pipeline.LastRequest?.MetadataFilter);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDelegateToPipeline_WithoutRequiringEmbeddingModelOrProviderResolution()
    {
        // Verifies that an unspecified embedding model does not block delegation: the tool forwards the query to the pipeline and lets it use its configured embedding, resolving no provider itself.
        var pipeline = new TrackingRetrievalPipeline();
        var tool = new DefaultVectorQueryTool(pipeline);
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
            EmbeddingModel = null,
        };

        var result = await tool.ExecuteAsync(request);

        Assert.True(pipeline.RetrieveWasCalled);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapSuccessfulRetrievalMatchesAndMetadataIntoToolResult()
    {
        // Verifies that a successful retrieval maps its matches and metadata straight into the tool result, preserving the existing retrieval item shape.
        var metadata = new RagMetadata(new Dictionary<string, string>
        {
            ["latencyMs"] = "12",
        });
        var pipeline = new TrackingRetrievalPipeline
        {
            ForcedResult = RetrievalResult.Success(
                [
                    new RetrievalResultItem
                    {
                        RecordId = "document-1:chunk:0",
                        Content = "retrieved chunk content",
                        Score = 0.91,
                        Metadata = new RagMetadata(new Dictionary<string, string>
                        {
                            ["source"] = "docs",
                        }),
                    },
                ],
                metadata),
        };
        var tool = new DefaultVectorQueryTool(pipeline);

        var result = await tool.ExecuteAsync(CreateRequest());

        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        var single = Assert.Single(result.Matches);
        Assert.Equal("document-1:chunk:0", single.RecordId);
        Assert.Equal("retrieved chunk content", single.Content);
        Assert.Equal(0.91, single.Score);
        Assert.Equal("docs", single.Metadata.Values["source"]);
        Assert.Same(metadata, result.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTreatEmptyRetrievalMatchesAsSuccess()
    {
        // Verifies that a retrieval that matched nothing is mapped to a successful, empty tool result rather than a failure.
        var pipeline = new TrackingRetrievalPipeline
        {
            ForcedResult = RetrievalResult.Success(),
        };
        var tool = new DefaultVectorQueryTool(pipeline);

        var result = await tool.ExecuteAsync(CreateRequest());

        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.Empty(result.Matches);
    }

    [Theory]
    [InlineData(RetrievalErrorCode.InvalidRequest)]
    [InlineData(RetrievalErrorCode.EmbeddingFailed)]
    [InlineData(RetrievalErrorCode.VectorStoreQueryFailed)]
    [InlineData(RetrievalErrorCode.RetrievalFailed)]
    public async Task ExecuteAsync_ShouldMapRetrievalFailureCodeAndReasonIntoToolResult(RetrievalErrorCode errorCode)
    {
        // Verifies that every provider-independent retrieval failure category and reason are surfaced through the tool result unchanged.
        var pipeline = new TrackingRetrievalPipeline
        {
            ForcedResult = RetrievalResult.Failure(errorCode, "Retrieval step failed."),
        };
        var tool = new DefaultVectorQueryTool(pipeline);

        var result = await tool.ExecuteAsync(CreateRequest());

        Assert.False(result.Succeeded);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal("Retrieval step failed.", result.Reason);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnInvalidRequestFailure_WhenRequestIsNull()
    {
        // Verifies that a null request is reported as a deterministic managed failure without invoking the retrieval pipeline.
        var pipeline = new TrackingRetrievalPipeline();
        var tool = new DefaultVectorQueryTool(pipeline);

        var result = await tool.ExecuteAsync(null!);

        Assert.False(pipeline.RetrieveWasCalled);
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ShouldReturnInvalidRequestFailure_WhenVectorStoreNameIsMissing(string? vectorStoreName)
    {
        // Verifies that a missing vector store name — which the retrieval pipeline cannot see — is reported by the tool as a managed failure before delegation.
        var pipeline = new TrackingRetrievalPipeline();
        var tool = new DefaultVectorQueryTool(pipeline);
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = vectorStoreName!,
            IndexName = "documents",
            QueryText = "What is Runiq?",
        };

        var result = await tool.ExecuteAsync(request);

        Assert.False(pipeline.RetrieveWasCalled);
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ShouldDelegateInvalidIndexNameToPipeline_AndMapItsFailure(string? indexName)
    {
        // Verifies that the tool does not duplicate the pipeline's index validation: it delegates and maps the pipeline's invalid-request failure through.
        var pipeline = new TrackingRetrievalPipeline
        {
            ForcedResult = RetrievalResult.Failure(RetrievalErrorCode.InvalidRequest, "Index name is required."),
        };
        var tool = new DefaultVectorQueryTool(pipeline);
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = indexName!,
            QueryText = "What is Runiq?",
        };

        var result = await tool.ExecuteAsync(request);

        Assert.True(pipeline.RetrieveWasCalled);
        Assert.Equal(indexName, pipeline.LastRequest?.IndexName);
        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationTokenToRetrievalPipeline()
    {
        // Verifies that the cancellation token is forwarded through the async call chain to the retrieval pipeline.
        var pipeline = new TrackingRetrievalPipeline();
        var tool = new DefaultVectorQueryTool(pipeline);
        using var cancellationTokenSource = new CancellationTokenSource();

        await tool.ExecuteAsync(CreateRequest(), cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, pipeline.LastCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowBeforeCallingPipeline_WhenCancellationIsAlreadyRequested()
    {
        // Verifies that an already-cancelled token throws before the retrieval pipeline is invoked, matching the retrieval cancellation standard.
        var pipeline = new TrackingRetrievalPipeline();
        var tool = new DefaultVectorQueryTool(pipeline);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            tool.ExecuteAsync(CreateRequest(), cancellationTokenSource.Token));

        Assert.False(pipeline.RetrieveWasCalled);
    }

    private static VectorQueryToolRequest CreateRequest()
    {
        return new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
        };
    }

    private sealed class TrackingRetrievalPipeline : IRagRetrievalPipeline
    {
        public bool RetrieveWasCalled { get; private set; }

        public RetrievalRequest? LastRequest { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public RetrievalResult? ForcedResult { get; set; }

        public Task<RetrievalResult> RetrieveAsync(
            RetrievalRequest request,
            CancellationToken cancellationToken = default)
        {
            RetrieveWasCalled = true;
            LastRequest = request;
            LastCancellationToken = cancellationToken;

            return Task.FromResult(ForcedResult ?? RetrievalResult.Success());
        }
    }
}

