using Runiq.AI.Rag.Abstractions.Embeddings;
using Runiq.AI.Rag.Abstractions.Retrieval;
using Runiq.AI.Rag.Abstractions.Services;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.Models.Context;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Embeddings;
using Runiq.AI.Rag.Models.Metadata;
using Runiq.AI.Rag.Models.Queries;
using Runiq.AI.Rag.Models.Search;
using Runiq.AI.Rag.Models.VectorStores;

namespace Runiq.AI.Rag.Tests.Abstractions;

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
        var record = new VectorRecord
        {
            Id = "vector-1",
            Values = [0.1f],
        };
        var embedding = new RagEmbedding([0.1f]);

        var createResult = await vectorStore.CreateIndexAsync(new CreateVectorIndexRequest
        {
            IndexName = "documents",
            Dimensions = embedding.Dimensions,
        });
        var upsertResult = await vectorStore.UpsertAsync(new UpsertVectorRequest
        {
            IndexName = "documents",
            Records = [record],
        });
        var queryResult = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = embedding.Values,
            TopK = 1,
        });
        var results = await vectorStore.SearchAsync(new RagQuery { Text = "query" }, embedding);

        Assert.True(createResult.Succeeded);
        Assert.Equal("documents", createResult.IndexName);
        Assert.True(upsertResult.Succeeded);
        Assert.Equal(1, upsertResult.UpsertedCount);
        var vectorResult = Assert.Single(queryResult.Records);
        Assert.Equal(record.Id, vectorResult.Id);
        Assert.Equal(1.0, vectorResult.Score);
        var result = Assert.Single(results);
        Assert.Equal(record.Id, result.Chunk.Id);
    }

    [Fact]
    public async Task IRagVectorStore_ChunkUpsertAsync_ShouldDelegateToRequestUpsertWithIndexName()
    {
        IRagVectorStore vectorStore = new RequestOnlyVectorStore();
        var chunk = new RagChunk
        {
            Id = "chunk-1",
            DocumentId = "document-1",
            Content = "chunk content",
            Index = 3,
            Metadata = new RagChunkMetadata
            {
                StartIndex = 10,
                EndIndex = 24,
                TokenCount = 4,
                AdditionalMetadata = new RagMetadata(new Dictionary<string, string>
                {
                    ["source"] = "docs",
                }),
            },
        };
        var embedding = new RagEmbedding([0.1f, 0.2f]);

        var result = await vectorStore.UpsertAsync("documents", chunk, embedding);
        var request = Assert.IsType<RequestOnlyVectorStore>(vectorStore).LastUpsertRequest;
        var record = Assert.Single(request!.Records!);

        Assert.True(result.Succeeded);
        Assert.Equal("documents", request.IndexName);
        Assert.Equal("chunk-1", record.Id);
        Assert.Equal([0.1f, 0.2f], record.Values);
        Assert.Equal("chunk content", record.Content);
        Assert.Equal("document-1", record.Metadata.Values["documentId"]);
        Assert.Equal("3", record.Metadata.Values["chunkIndex"]);
        Assert.Equal("10", record.Metadata.Values["startIndex"]);
        Assert.Equal("24", record.Metadata.Values["endIndex"]);
        Assert.Equal("4", record.Metadata.Values["tokenCount"]);
        Assert.Equal("docs", record.Metadata.Values["source"]);
    }

    [Fact]
    public async Task IRagVectorStore_ChunkUpsertAsync_ShouldPassCancellationTokenToRequestUpsert()
    {
        IRagVectorStore vectorStore = new RequestOnlyVectorStore();
        using var cancellationTokenSource = new CancellationTokenSource();

        await vectorStore.UpsertAsync(
            "documents",
            new RagChunk
            {
                Id = "chunk-1",
                DocumentId = "document-1",
                Content = "chunk content",
            },
            new RagEmbedding([0.1f, 0.2f]),
            cancellationTokenSource.Token);

        Assert.Equal(
            cancellationTokenSource.Token,
            Assert.IsType<RequestOnlyVectorStore>(vectorStore).LastUpsertCancellationToken);
    }

    [Fact]
    public async Task IRagVectorStore_DefaultQueryAsync_ShouldReturnFailedNotImplementedResult()
    {
        IRagVectorStore vectorStore = new RequestOnlyVectorStore();

        var result = await vectorStore.QueryAsync(new QueryVectorRequest
        {
            IndexName = "documents",
            Values = [0.1f, 0.2f],
        });

        Assert.False(result.Succeeded);
        Assert.Empty(result.Records);
        Assert.Equal("Query operation is not implemented by this vector store provider.", result.Reason);
    }

    [Fact]
    public async Task IRagVectorStore_DefaultDeleteAsync_ShouldReturnFailedNotImplementedResult()
    {
        IRagVectorStore vectorStore = new RequestOnlyVectorStore();

        var result = await vectorStore.DeleteAsync(new DeleteVectorRequest
        {
            IndexName = "documents",
            VectorIds = ["vector-1"],
        });

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.RequestedCount);
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal("vector-1", Assert.Single(result.NotFoundVectorIds));
        Assert.Equal("Delete operation is not implemented by this vector store provider.", result.Reason);
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
        private VectorRecord? record;

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
            var records = request.Records!;
            record = records.Single();

            return Task.FromResult(new UpsertVectorResult
            {
                Succeeded = true,
                UpsertedCount = records.Count,
                VectorIds = records.Select(item => item.Id).ToList(),
            });
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RagSearchResult> results = record is null
                ? []
                :
                [
                    new RagSearchResult
                    {
                        Chunk = new RagChunk
                        {
                            Id = record.Id,
                            DocumentId = "document-1",
                        },
                        Score = 1.0,
                    },
                ];

            return Task.FromResult(results);
        }

        public Task<QueryVectorResult> QueryAsync(
            QueryVectorRequest request,
            CancellationToken cancellationToken = default)
        {
            var records = record is null
                ? new List<VectorSearchResult>()
                :
                [
                    new VectorSearchResult
                    {
                        Id = record.Id,
                        Score = 1.0,
                        Metadata = record.Metadata,
                    },
                ];

            return Task.FromResult(new QueryVectorResult
            {
                Succeeded = true,
                Records = records,
            });
        }
    }

    private sealed class RequestOnlyVectorStore : IRagVectorStore
    {
        public UpsertVectorRequest? LastUpsertRequest { get; private set; }

        public CancellationToken LastUpsertCancellationToken { get; private set; }

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
            LastUpsertRequest = request;
            LastUpsertCancellationToken = cancellationToken;

            return Task.FromResult(new UpsertVectorResult
            {
                Succeeded = true,
                UpsertedCount = request.Records?.Count ?? 0,
                VectorIds = request.Records?.Select(record => record.Id).ToList() ?? [],
            });
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RagSearchResult>>(Array.Empty<RagSearchResult>());
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

