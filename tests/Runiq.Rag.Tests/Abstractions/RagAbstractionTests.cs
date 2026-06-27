using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Abstractions.Retrieval;
using Runiq.Rag.Abstractions.Services;
using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.Models.Context;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;

namespace Runiq.Rag.Tests.Abstractions;

public sealed class RagAbstractionTests
{
    [Fact]
    public void RagEmbedding_DefaultValues_ShouldNotBeNullAndShouldBeEmpty()
    {
        var embedding = new RagEmbedding();

        Assert.NotNull(embedding.Values);
        Assert.Empty(embedding.Values);
    }

    [Fact]
    public void RagEmbedding_Dimensions_ShouldReturnValueCount()
    {
        var embedding = new RagEmbedding([0.1f, 0.2f, 0.3f]);

        Assert.Equal(3, embedding.Dimensions);
    }

    [Fact]
    public async Task IRagEmbeddingProvider_ShouldAllowTestImplementation()
    {
        IRagEmbeddingProvider provider = new TestEmbeddingProvider();

        var embedding = await provider.GenerateAsync("query");

        Assert.Equal(2, embedding.Dimensions);
    }

    [Fact]
    public async Task IRagVectorStore_ShouldAllowTestImplementation()
    {
        IRagVectorStore vectorStore = new TestVectorStore();
        var chunk = new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
        };
        var embedding = new RagEmbedding([0.1f]);

        await vectorStore.UpsertAsync(chunk, embedding);
        var results = await vectorStore.SearchAsync(new RagQuery { Text = "query" }, embedding);

        var result = Assert.Single(results);
        Assert.Same(chunk, result.Chunk);
    }

    [Fact]
    public async Task IRagRetriever_ShouldAllowTestImplementation()
    {
        IRagRetriever retriever = new TestRetriever();

        var results = await retriever.RetrieveAsync(new RagQuery { Text = "query" });

        Assert.Single(results);
    }

    [Fact]
    public async Task IRagService_ShouldAllowTestImplementation()
    {
        IRagService service = new TestRagService();

        var context = await service.GetContextAsync(new RagQuery { Text = "query" });

        Assert.Equal("query", context.Query.Text);
    }

    private sealed class TestEmbeddingProvider : IRagEmbeddingProvider
    {
        public Task<RagEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RagEmbedding([1.0f, 2.0f]));
        }
    }

    private sealed class TestVectorStore : IRagVectorStore
    {
        private RagChunk? chunk;

        public Task UpsertAsync(
            RagChunk chunk,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            this.chunk = chunk;

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RagSearchResult> results = chunk is null
                ? []
                :
                [
                    new RagSearchResult
                    {
                        Chunk = chunk,
                        Score = 1.0,
                    },
                ];

            return Task.FromResult(results);
        }
    }

    private sealed class TestRetriever : IRagRetriever
    {
        public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
            RagQuery query,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RagSearchResult> results =
            [
                new RagSearchResult
                {
                    Chunk = new RagChunk
                    {
                        Id = "chunk-1",
                        DocumentId = "document-1",
                    },
                    Score = 1.0,
                },
            ];

            return Task.FromResult(results);
        }
    }

    private sealed class TestRagService : IRagService
    {
        public Task<RagContext> GetContextAsync(
            RagQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RagContext
            {
                Query = query,
            });
        }
    }
}
