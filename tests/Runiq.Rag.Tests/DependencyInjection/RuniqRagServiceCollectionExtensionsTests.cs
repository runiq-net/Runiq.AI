using Microsoft.Extensions.DependencyInjection;
using Runiq.Rag.Abstractions.Embeddings;
using Runiq.Rag.Abstractions.Retrieval;
using Runiq.Rag.Abstractions.Services;
using Runiq.Rag.Abstractions.VectorStores;
using Runiq.Rag.DependencyInjection;
using Runiq.Rag.Embeddings;
using Runiq.Rag.Models.Documents;
using Runiq.Rag.Models.Embeddings;
using Runiq.Rag.Models.Queries;
using Runiq.Rag.Models.Search;
using Runiq.Rag.VectorStores;

namespace Runiq.Rag.Tests.DependencyInjection;

public sealed class RuniqRagServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRuniqRag_ShouldThrow_WhenServicesIsNull()
    {
        IServiceCollection services = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => services.AddRuniqRag());

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagService()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagService));
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagRetriever()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagRetriever));
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagEmbeddingProvider()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagEmbeddingProvider));
    }

    [Fact]
    public void AddRuniqRag_ShouldRegisterRagVectorStore()
    {
        var services = new ServiceCollection();

        services.AddRuniqRag();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRagVectorStore));
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveRagService()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IRagService>());
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveRagRetriever()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<IRagRetriever>());
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveNullEmbeddingProviderByDefault()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<NullEmbeddingProvider>(serviceProvider.GetRequiredService<IRagEmbeddingProvider>());
    }

    [Fact]
    public void AddRuniqRag_ShouldResolveNullVectorStoreByDefault()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<NullVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    [Fact]
    public void AddRuniqRag_ShouldNotOverwriteUserRegisteredEmbeddingProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRagEmbeddingProvider, TestEmbeddingProvider>();

        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestEmbeddingProvider>(serviceProvider.GetRequiredService<IRagEmbeddingProvider>());
    }

    [Fact]
    public void AddRuniqRag_ShouldNotOverwriteUserRegisteredVectorStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRagVectorStore, TestVectorStore>();

        services.AddRuniqRag();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<TestVectorStore>(serviceProvider.GetRequiredService<IRagVectorStore>());
    }

    private sealed class TestEmbeddingProvider : IRagEmbeddingProvider
    {
        public Task<RagEmbedding> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RagEmbedding());
        }
    }

    private sealed class TestVectorStore : IRagVectorStore
    {
        public Task UpsertAsync(
            RagChunk chunk,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
            RagQuery query,
            RagEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RagSearchResult>>(Array.Empty<RagSearchResult>());
        }
    }
}
