using Runiq.Rag.Models.Metadata;
using Runiq.Rag.Models.Retrieval;

namespace Runiq.Rag.Tests.Models;

public sealed class RetrievalModelTests
{
    [Fact]
    public void Constructor_ShouldStoreIndexName_WhenRequestIsValid()
    {
        // Verifies that a valid retrieval request preserves the target vector store index name.
        var request = new RetrievalRequest
        {
            IndexName = "documents",
            QueryText = "What is Runiq?",
        };

        Assert.Equal("documents", request.IndexName);
    }

    [Fact]
    public void Constructor_ShouldStoreQueryText_WhenProvided()
    {
        // Verifies that the request carries the natural-language query text supplied by the caller.
        var request = new RetrievalRequest
        {
            IndexName = "documents",
            QueryText = "What is Runiq?",
        };

        Assert.Equal("What is Runiq?", request.QueryText);
        Assert.True(request.HasRetrievableQuery);
    }

    [Fact]
    public void Constructor_ShouldStoreQueryVector_WhenProvided()
    {
        // Verifies that the request carries a pre-computed query vector supplied by the caller.
        var request = new RetrievalRequest
        {
            IndexName = "documents",
            QueryVector = [0.1f, 0.2f, 0.3f],
        };

        Assert.Equal([0.1f, 0.2f, 0.3f], request.QueryVector);
        Assert.True(request.HasRetrievableQuery);
    }

    [Fact]
    public void Constructor_ShouldDefaultTopKToFive_WhenNotSpecified()
    {
        // Verifies that the request applies the repository-standard default TopK of five.
        var request = new RetrievalRequest
        {
            IndexName = "documents",
            QueryText = "What is Runiq?",
        };

        Assert.Equal(5, request.TopK);
    }

    [Fact]
    public void Constructor_ShouldStoreTopK_WhenValid()
    {
        // Verifies that the request preserves an explicit, positive TopK value.
        var request = new RetrievalRequest
        {
            IndexName = "documents",
            QueryText = "What is Runiq?",
            TopK = 10,
        };

        Assert.Equal(10, request.TopK);
    }

    [Fact]
    public void Constructor_ShouldStoreMetadataFilter_WhenProvided()
    {
        // Verifies that the request carries the provider-independent metadata filter supplied by the caller.
        var filter = new RetrievalMetadataFilter(new Dictionary<string, string>
        {
            ["tenant"] = "runiq",
        });

        var request = new RetrievalRequest
        {
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
    public void MetadataFilter_ShouldDefaultToEmpty_WhenNotSpecified()
    {
        // Verifies that an unspecified metadata filter is a non-null, empty filter that constrains nothing.
        var request = new RetrievalRequest
        {
            IndexName = "documents",
            QueryText = "What is Runiq?",
        };

        Assert.NotNull(request.MetadataFilter);
        Assert.True(request.MetadataFilter.IsEmpty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldCarryInvalidIndexNameWithoutThrowing(string? indexName)
    {
        // Verifies that the request is a plain carrier contract: an invalid index name is stored as supplied
        // and reported by the retrieval pipeline as a managed invalid request failure, not as an exception.
        var request = new RetrievalRequest
        {
            IndexName = indexName!,
            QueryText = "What is Runiq?",
        };

        Assert.Equal(indexName, request.IndexName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ShouldCarryInvalidTopKWithoutThrowing(int topK)
    {
        // Verifies that the request is a plain carrier contract: a zero or negative TopK is stored as supplied
        // and reported by the retrieval pipeline as a managed invalid request failure, not as an exception.
        var request = new RetrievalRequest
        {
            IndexName = "documents",
            QueryText = "What is Runiq?",
            TopK = topK,
        };

        Assert.Equal(topK, request.TopK);
    }

    [Fact]
    public void Constructor_ShouldRejectNullMetadataFilter_Deterministically()
    {
        // Verifies that a null metadata filter is rejected rather than silently accepted.
        var exception = Assert.Throws<ArgumentNullException>(() => new RetrievalRequest
        {
            IndexName = "documents",
            QueryText = "What is Runiq?",
            MetadataFilter = null!,
        });

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void HasRetrievableQuery_ShouldBeFalse_WhenNeitherTextNorVectorIsMeaningful()
    {
        // Verifies that a request with no query text and no query vector is recognized as semantically empty.
        var request = new RetrievalRequest
        {
            IndexName = "documents",
        };

        Assert.False(request.HasRetrievableQuery);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasRetrievableQuery_ShouldIgnoreWhitespaceOnlyQueryText(string? queryText)
    {
        // Verifies that whitespace-only query text without a vector is not treated as retrievable data.
        var request = new RetrievalRequest
        {
            IndexName = "documents",
            QueryText = queryText,
        };

        Assert.False(request.HasRetrievableQuery);
    }

    [Fact]
    public void HasRetrievableQuery_ShouldBeFalse_WhenQueryVectorIsEmpty()
    {
        // Verifies that an empty query vector without query text is not treated as retrievable data.
        var request = new RetrievalRequest
        {
            IndexName = "documents",
            QueryVector = [],
        };

        Assert.False(request.HasRetrievableQuery);
    }

    [Fact]
    public void RetrievalMetadataFilter_ShouldBuildFromExactMatchPairs_WithoutProviderSyntax()
    {
        // Verifies that key/value pairs are mapped to provider-independent equality criteria without any
        // provider-specific query syntax.
        var filter = new RetrievalMetadataFilter(new Dictionary<string, string>
        {
            ["tenant"] = "runiq",
            ["category"] = "release-notes",
        });

        Assert.False(filter.IsEmpty);
        Assert.Equal(2, filter.Criteria.Count);
        Assert.All(filter.Criteria, criterion => Assert.Equal(RetrievalMetadataFilterOperator.Equal, criterion.Operator));
        Assert.Contains(filter.Criteria, criterion => criterion.Key == "tenant" && criterion.Value == "runiq");
        Assert.Contains(filter.Criteria, criterion => criterion.Key == "category" && criterion.Value == "release-notes");
    }

    [Fact]
    public void RetrievalMetadataFilter_ShouldBuildFromCriteria_AndPreserveOperator()
    {
        // Verifies that a filter built from explicit criteria preserves each criterion's key, value, and operator.
        var filter = new RetrievalMetadataFilter(
        [
            new RetrievalMetadataFilterCriterion("tenant", "runiq", RetrievalMetadataFilterOperator.Equal),
        ]);

        var criterion = Assert.Single(filter.Criteria);
        Assert.Equal("tenant", criterion.Key);
        Assert.Equal("runiq", criterion.Value);
        Assert.Equal(RetrievalMetadataFilterOperator.Equal, criterion.Operator);
    }

    [Fact]
    public void RetrievalMetadataFilter_ShouldCopyEqualityFilters_ToRemainImmutable()
    {
        // Verifies that the filter copies its source dictionary so later mutation cannot alter the filter.
        var source = new Dictionary<string, string>
        {
            ["tenant"] = "runiq",
        };

        var filter = new RetrievalMetadataFilter(source);
        source["tenant"] = "changed";

        Assert.Equal("runiq", Assert.Single(filter.Criteria).Value);
    }

    [Fact]
    public void RetrievalMetadataFilter_Empty_ShouldApplyNoConstraints()
    {
        // Verifies that the shared empty filter represents "no filtering".
        Assert.True(RetrievalMetadataFilter.Empty.IsEmpty);
        Assert.Empty(RetrievalMetadataFilter.Empty.Criteria);
    }

    [Fact]
    public void RetrievalMetadataFilter_ShouldRejectNullEqualityFilters_Deterministically()
    {
        // Verifies that constructing a filter from a null key/value pair collection is rejected rather than
        // silently accepted.
        Assert.Throws<ArgumentNullException>(() =>
            new RetrievalMetadataFilter((IEnumerable<KeyValuePair<string, string>>)null!));
    }

    [Fact]
    public void RetrievalMetadataFilter_ShouldRejectNullCriteriaCollection_Deterministically()
    {
        // Verifies that constructing a filter from a null criteria collection is rejected rather than
        // silently accepted.
        Assert.Throws<ArgumentNullException>(() =>
            new RetrievalMetadataFilter((IEnumerable<RetrievalMetadataFilterCriterion>)null!));
    }

    [Fact]
    public void RetrievalMetadataFilter_ShouldRejectNullCriterionEntry_Deterministically()
    {
        // Verifies that a criteria collection containing a null criterion fails fast at construction, so a
        // filter can never carry a null criterion into a vector store query.
        var exception = Assert.Throws<ArgumentException>(() => new RetrievalMetadataFilter(
        [
            new RetrievalMetadataFilterCriterion("tenant", "runiq"),
            null!,
        ]));

        Assert.Equal("criteria", exception.ParamName);
    }

    [Fact]
    public void RetrievalMetadataFilterCriterion_ShouldDefaultToEqualityOperator()
    {
        // Verifies that a criterion built without an explicit operator defaults to exact-match equality.
        var criterion = new RetrievalMetadataFilterCriterion("tenant", "runiq");

        Assert.Equal("tenant", criterion.Key);
        Assert.Equal("runiq", criterion.Value);
        Assert.Equal(RetrievalMetadataFilterOperator.Equal, criterion.Operator);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RetrievalMetadataFilterCriterion_ShouldFailFast_WhenKeyIsInvalid(string? key)
    {
        // Verifies that null, empty, and whitespace metadata keys are rejected deterministically at
        // construction, so an invalid key can never reach a vector store query.
        var exception = Assert.Throws<ArgumentException>(() =>
            new RetrievalMetadataFilterCriterion(key!, "runiq"));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void RetrievalMetadataFilterCriterion_ShouldFailFast_WhenValueIsNull()
    {
        // Verifies that a null expected metadata value is rejected deterministically at construction.
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new RetrievalMetadataFilterCriterion("tenant", null!));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void RetrievalResult_ShouldReturnItems_WhenRetrievalSucceeds()
    {
        // Verifies that a successful retrieval result exposes its retrieved item list.
        var result = RetrievalResult.Success(
            [
                new RetrievalResultItem { Content = "chunk one", Score = 0.9 },
                new RetrievalResultItem { Content = "chunk two", Score = 0.8 },
            ]);

        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(["chunk one", "chunk two"], result.Items.Select(item => item.Content));
    }

    [Fact]
    public void RetrievalResult_EmptyResult_ShouldBeRepresentedAsSuccess()
    {
        // Verifies that an empty retrieval result is a success and not an error.
        var result = RetrievalResult.Success();

        Assert.True(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
        Assert.NotNull(result.Items);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void RetrievalResult_ShouldNormalizeNullItems_ToEmptyCollection()
    {
        // Verifies that a null item collection is normalized to an empty, iterable collection.
        var result = RetrievalResult.Success(null);

        Assert.NotNull(result.Items);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void RetrievalResult_ShouldRepresentInvalidRequest_Deterministically()
    {
        // Verifies that an invalid request failure is represented with a deterministic error code and reason.
        var result = RetrievalResult.Failure(
            RetrievalErrorCode.InvalidRequest,
            "Request carried neither query text nor a query vector.");

        Assert.False(result.Succeeded);
        Assert.Equal(RetrievalErrorCode.InvalidRequest, result.ErrorCode);
        Assert.Equal("Request carried neither query text nor a query vector.", result.Reason);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void RetrievalResult_Success_ShouldUseNoneErrorCodeAndEmptyReason()
    {
        // Verifies that the success factory always produces the standard successful result shape.
        var result = RetrievalResult.Success();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Metadata);
        Assert.Equal(string.Empty, result.Reason);
        Assert.Equal(RetrievalErrorCode.None, result.ErrorCode);
    }

    [Fact]
    public void RetrievalResult_Failure_ShouldRejectNoneErrorCode()
    {
        // Verifies that failed retrieval results cannot be created with the success-only None error code.
        var exception = Assert.Throws<ArgumentException>(() => RetrievalResult.Failure(
            RetrievalErrorCode.None,
            "Retrieval failed."));

        Assert.Equal("errorCode", exception.ParamName);
    }

    [Fact]
    public void RetrievalResult_Failure_ShouldRejectNullReason()
    {
        // Verifies that failed retrieval results always carry a non-null reason string.
        var exception = Assert.Throws<ArgumentNullException>(() => RetrievalResult.Failure(
            RetrievalErrorCode.RetrievalFailed,
            null!));

        Assert.Equal("reason", exception.ParamName);
    }

    [Fact]
    public void RetrievalResult_Failure_ShouldExposeNonNullMetadata()
    {
        // Verifies that failed retrieval results expose non-null metadata even when no metadata is supplied.
        var result = RetrievalResult.Failure(RetrievalErrorCode.RetrievalFailed, "Store rejected the query.");

        Assert.NotNull(result.Metadata);
        Assert.Empty(result.Metadata.Values);
    }

    [Fact]
    public void RetrievalResultItem_ShouldCarryContentMetadataAndSimilarityScore()
    {
        // Verifies that a result item preserves chunk content, chunk metadata, and its similarity score.
        var item = new RetrievalResultItem
        {
            Content = "retrieved chunk content",
            Score = 0.97,
            Metadata = new RagMetadata(new Dictionary<string, string>
            {
                ["source"] = "docs",
            }),
        };

        Assert.Equal("retrieved chunk content", item.Content);
        Assert.Equal(0.97, item.Score);
        Assert.Equal("docs", item.Metadata.Values["source"]);
    }

    [Fact]
    public void RetrievalResultItem_DefaultMetadata_ShouldNotBeNull()
    {
        // Verifies that a result item exposes non-null, empty metadata by default.
        var item = new RetrievalResultItem();

        Assert.NotNull(item.Metadata);
        Assert.Empty(item.Metadata.Values);
        Assert.Equal(string.Empty, item.Content);
    }

    [Fact]
    public void RetrievalResultItem_ShouldRejectNullMetadata_Deterministically()
    {
        // Verifies that a null metadata value on a result item is rejected rather than silently accepted.
        var exception = Assert.Throws<ArgumentNullException>(() => new RetrievalResultItem
        {
            Metadata = null!,
        });

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void RetrievalResultItem_ShouldCarryRecordId()
    {
        // Verifies that a result item preserves the provider-independent record id of the retrieved chunk.
        var item = new RetrievalResultItem
        {
            RecordId = "document-1:chunk:0",
            Content = "retrieved chunk content",
        };

        Assert.Equal("document-1:chunk:0", item.RecordId);
    }

    [Fact]
    public void RetrievalResultItem_DefaultRecordId_ShouldBeEmpty()
    {
        // Verifies that a result item exposes a non-null, empty record id by default.
        var item = new RetrievalResultItem();

        Assert.Equal(string.Empty, item.RecordId);
    }

    [Theory]
    [InlineData(RetrievalErrorCode.EmbeddingFailed)]
    [InlineData(RetrievalErrorCode.VectorStoreQueryFailed)]
    public void RetrievalResult_Failure_ShouldSupportPipelineErrorCodes(RetrievalErrorCode errorCode)
    {
        // Verifies that the retrieval pipeline's embedding and vector store failure categories produce failed results.
        var result = RetrievalResult.Failure(errorCode, "Retrieval pipeline step failed.");

        Assert.False(result.Succeeded);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Empty(result.Items);
    }
}
