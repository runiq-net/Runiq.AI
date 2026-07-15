using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Retrieval;
using Runiq.AI.Rag.Models.Tools;

namespace Runiq.AI.Rag.Tests.Models;

public sealed class VectorQueryToolModelTests
{
    [Fact]
    public void Request_ShouldStoreVectorStoreNameIndexNameAndQuery_WhenProvided()
    {
        // Verifies that the tool request carries the vector store name, index name, and user query text supplied
        // by the agent so the implementation can associate the invocation with a store and index.
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
        };

        Assert.Equal("primary", request.VectorStoreName);
        Assert.Equal("documents", request.IndexName);
        Assert.Equal("What is Runiq?", request.QueryText);
    }

    [Fact]
    public void Request_ShouldStoreEmbeddingModel_WhenProvided()
    {
        // Verifies that the request carries the embedding model identifier the tool should use for the query.
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
            EmbeddingModel = "text-embedding-3-small",
        };

        Assert.Equal("text-embedding-3-small", request.EmbeddingModel);
    }

    [Fact]
    public void Request_EmbeddingModel_ShouldDefaultToNull_WhenNotSpecified()
    {
        // Verifies that an unspecified embedding model is null so the implementation can fall back to the
        // pipeline's configured embedding model.
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
        };

        Assert.Null(request.EmbeddingModel);
    }

    [Fact]
    public void Request_ShouldDefaultTopKToFive_WhenNotSpecified()
    {
        // Verifies that the request applies the repository-standard default TopK of five.
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
        };

        Assert.Equal(5, request.TopK);
    }

    [Fact]
    public void Request_ShouldCarryTopK_WhenSpecified()
    {
        // Verifies that the request preserves an explicit TopK value for forwarding to the retrieval pipeline.
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
            TopK = 12,
        };

        Assert.Equal(12, request.TopK);
    }

    [Fact]
    public void Request_ShouldStoreMetadataFilter_WhenProvided()
    {
        // Verifies that the request carries the existing provider-independent metadata filter type unchanged.
        var filter = new RetrievalMetadataFilter(new Dictionary<string, string>
        {
            ["tenant"] = "runiq",
        });

        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
            MetadataFilter = filter,
        };

        Assert.Same(filter, request.MetadataFilter);
        var criterion = Assert.Single(request.MetadataFilter.Criteria);
        Assert.Equal("tenant", criterion.Key);
        Assert.Equal("runiq", criterion.Value);
    }

    [Fact]
    public void Request_MetadataFilter_ShouldDefaultToEmpty_WhenNotSpecified()
    {
        // Verifies that an unspecified metadata filter is a non-null, empty filter that constrains nothing.
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
        };

        Assert.NotNull(request.MetadataFilter);
        Assert.True(request.MetadataFilter.IsEmpty);
    }

    [Fact]
    public void Request_ShouldRejectNullMetadataFilter_Deterministically()
    {
        // Verifies that a null metadata filter is rejected rather than silently accepted, matching the
        // contract-level nullability of the existing retrieval request.
        var exception = Assert.Throws<ArgumentNullException>(() => new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
            MetadataFilter = null!,
        });

        Assert.Equal("value", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_ShouldCarryInvalidVectorStoreNameWithoutThrowing(string? vectorStoreName)
    {
        // Verifies that the request is a plain carrier contract: an invalid vector store name is stored as
        // supplied and left for the implementation to report as a managed failure, not as an exception.
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = vectorStoreName!,
            IndexName = "documents",
            QueryText = "What is Runiq?",
        };

        Assert.Equal(vectorStoreName, request.VectorStoreName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_ShouldCarryInvalidIndexNameWithoutThrowing(string? indexName)
    {
        // Verifies that an invalid index name is stored as supplied and left for the retrieval path to report as
        // a managed failure rather than an exception.
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = indexName!,
            QueryText = "What is Runiq?",
        };

        Assert.Equal(indexName, request.IndexName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_ShouldCarryEmptyQueryTextWithoutThrowing(string? queryText)
    {
        // Verifies that a semantically empty query is stored as supplied and left for the retrieval path to
        // report as a managed failure rather than an exception.
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = queryText!,
        };

        Assert.Equal(queryText, request.QueryText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Request_ShouldCarryInvalidTopKWithoutThrowing(int topK)
    {
        // Verifies that a zero or negative TopK is stored as supplied and left for the retrieval pipeline to
        // report as a managed failure rather than an exception.
        var request = new VectorQueryToolRequest
        {
            VectorStoreName = "primary",
            IndexName = "documents",
            QueryText = "What is Runiq?",
            TopK = topK,
        };

        Assert.Equal(topK, request.TopK);
    }

    [Fact]
    public void Result_Success_ShouldExposeMatchesInRetrievalShape()
    {
        // Verifies that a successful result exposes matches reusing the existing retrieval item shape, preserving
        // record id, content, score, and metadata for agent consumption.
        var match = new RetrievalResultItem
        {
            RecordId = "document-1:chunk:0",
            Content = "retrieved chunk content",
            Score = 0.91,
            Metadata = new RagMetadata(new Dictionary<string, string>
            {
                ["source"] = "docs",
            }),
        };

        var result = VectorQueryToolResult.Success([match]);

        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.Equal(string.Empty, result.Reason);
        var single = Assert.Single(result.Matches);
        Assert.Equal("document-1:chunk:0", single.RecordId);
        Assert.Equal("retrieved chunk content", single.Content);
        Assert.Equal(0.91, single.Score);
        Assert.Equal("docs", single.Metadata.Values["source"]);
    }

    [Fact]
    public void Result_Success_ShouldTreatEmptyMatchesAsSuccess()
    {
        // Verifies that an empty match list is a success and not an error.
        var result = VectorQueryToolResult.Success();

        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.NotNull(result.Matches);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Result_Success_ShouldNormalizeNullMatches_ToEmptyCollection()
    {
        // Verifies that a null match collection is normalized to an empty, iterable collection.
        var result = VectorQueryToolResult.Success(null);

        Assert.NotNull(result.Matches);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Result_Success_ShouldExposeNonNullMetadata_WhenNoneSupplied()
    {
        // Verifies that a successful result exposes non-null, empty metadata even when no metadata is supplied.
        var result = VectorQueryToolResult.Success();

        Assert.NotNull(result.Metadata);
        Assert.Empty(result.Metadata.Values);
    }

    [Fact]
    public void Result_Failure_ShouldReuseRetrievalErrorCode_Deterministically()
    {
        // Verifies that a failed result reuses the existing retrieval error categories rather than a duplicate
        // enum, and carries the provider-independent reason with an empty match list.
        var result = VectorQueryToolResult.Failure(
            RetrievalErrorCode.InvalidRequest,
            "Request carried an empty query.");

        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, result.ErrorCode);
        Assert.Equal("Request carried an empty query.", result.Reason);
        Assert.Empty(result.Matches);
    }

    [Theory]
    [InlineData(RetrievalErrorCode.EmbeddingFailed)]
    [InlineData(RetrievalErrorCode.VectorStoreQueryFailed)]
    [InlineData(RetrievalErrorCode.RetrievalFailed)]
    public void Result_Failure_ShouldSupportRetrievalPipelineErrorCodes(RetrievalErrorCode errorCode)
    {
        // Verifies that the tool result can represent every retrieval pipeline failure category the
        // implementation may need to surface to the agent.
        var result = VectorQueryToolResult.Failure(errorCode, "Retrieval step failed.");

        Assert.False(result.Succeeded);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Result_Failure_ShouldRejectNoneErrorCode()
    {
        // Verifies that failed results cannot be created with the success-only None error code.
        var exception = Assert.Throws<ArgumentException>(() => VectorQueryToolResult.Failure(
            RetrievalErrorCode.None,
            "Invocation failed."));

        Assert.Equal("errorCode", exception.ParamName);
    }

    [Fact]
    public void Result_Failure_ShouldRejectNullReason()
    {
        // Verifies that failed results always carry a non-null reason string.
        var exception = Assert.Throws<ArgumentNullException>(() => VectorQueryToolResult.Failure(
            RetrievalErrorCode.RetrievalFailed,
            null!));

        Assert.Equal("reason", exception.ParamName);
    }
}

