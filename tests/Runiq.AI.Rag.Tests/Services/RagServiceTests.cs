using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Services;

namespace Runiq.AI.Rag.Tests.Services;

public sealed class RagServiceTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenRetrieverIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new RagService(null!));

        Assert.Equal("retriever", exception.ParamName);
    }

    [Fact]
    public async Task GetContextAsync_ShouldCallRetriever()
    {
        var retriever = new TrackingRetriever([]);
        var service = new RagService(retriever);
        var query = new RagQuery { Text = "query" };

        await service.GetContextAsync(query);

        Assert.True(retriever.WasCalled);
    }

    [Fact]
    public async Task GetContextAsync_ShouldReturnOriginalQuery()
    {
        var retriever = new TrackingRetriever([]);
        var service = new RagService(retriever);
        var query = new RagQuery { Text = "query" };

        var context = await service.GetContextAsync(query);

        Assert.Same(query, context.Query);
    }

    [Fact]
    public async Task GetContextAsync_ShouldReturnRetrievedResults()
    {
        var results = new List<RagSearchResult>
        {
            new()
            {
                Chunk = new RagChunk
                {
                    Id = "chunk-1",
                    DocumentId = "document-1",
                    Content = "First chunk",
                },
            },
        };
        var retriever = new TrackingRetriever(results);
        var service = new RagService(retriever);

        var context = await service.GetContextAsync(new RagQuery { Text = "query" });

        Assert.Single(context.Results);
        Assert.Same(results[0], context.Results[0]);
    }

    [Fact]
    public async Task GetContextAsync_ShouldReturnEmptyContent_WhenThereAreNoResults()
    {
        var retriever = new TrackingRetriever([]);
        var service = new RagService(retriever);

        var context = await service.GetContextAsync(new RagQuery { Text = "query" });

        Assert.Equal(string.Empty, context.Content);
    }

    [Fact]
    public async Task GetContextAsync_ShouldJoinChunkContentWithNewLine_WhenResultsExist()
    {
        var retriever = new TrackingRetriever(
        [
            new RagSearchResult
            {
                Chunk = new RagChunk
                {
                    Id = "chunk-1",
                    DocumentId = "document-1",
                    Content = "First chunk",
                },
            },
            new RagSearchResult
            {
                Chunk = new RagChunk
                {
                    Id = "chunk-2",
                    DocumentId = "document-1",
                    Content = "Second chunk",
                },
            },
        ]);
        var service = new RagService(retriever);

        var context = await service.GetContextAsync(new RagQuery { Text = "query" });

        Assert.Equal($"First chunk{Environment.NewLine}Second chunk", context.Content);
    }

    private sealed class TrackingRetriever : IRagRetriever
    {
        private readonly IReadOnlyList<RagSearchResult> results;

        public TrackingRetriever(IReadOnlyList<RagSearchResult> results)
        {
            this.results = results;
        }

        public bool WasCalled { get; private set; }

        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            return Task.FromResult(results);
        }
    }
}

