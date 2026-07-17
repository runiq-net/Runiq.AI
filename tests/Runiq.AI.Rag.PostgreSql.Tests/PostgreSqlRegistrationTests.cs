using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.PostgreSql;
using Runiq.AI.Rag.PostgreSql.DependencyInjection;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.PostgreSql.Documents;
using Runiq.AI.Core.AI.Embeddings;
using Runiq.AI.Core.Models;

namespace Runiq.AI.Rag.PostgreSql.Tests;

public sealed class PostgreSqlRegistrationTests
{
    // Verifies that the PostgreSQL convenience API selects default pgvector metadata without resolving a connection.
    [Fact]
    public void UsePostgreSqlVectorStore_ShouldSelectDefaultReference()
    {
        var registration = Register(index => index.UsePostgreSqlVectorStore());

        Assert.Equal("postgresql", registration.VectorStoreReference);
        Assert.Equal("PostgreSql", registration.VectorStoreType);
        Assert.Null(registration.NamedVectorStoreReference);
    }

    // Verifies that the PostgreSQL convenience API preserves a safe named store reference.
    [Fact]
    public void UsePostgreSqlVectorStore_ShouldSelectNamedReference()
    {
        var registration = Register(index => index.UsePostgreSqlVectorStore("corporate-store"));

        Assert.Equal("postgresql/corporate-store", registration.VectorStoreReference);
        Assert.Equal("corporate-store", registration.NamedVectorStoreReference);
    }

    // Verifies that empty named PostgreSQL store references fail during composition.
    [Fact]
    public void UsePostgreSqlVectorStore_ShouldRejectEmptyName() =>
        Assert.Throws<ArgumentException>(() => Register(index => index.UsePostgreSqlVectorStore(" ")));

    private static RagIndexRegistration Register(Action<RagIndexBuilder> selectStore)
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index =>
        {
            index.UseDirectory("documents").UseEmbeddingModel("model");
            selectStore(index);
        }));
        using var provider = services.BuildServiceProvider();
        return Assert.Single(provider.GetRequiredService<IRagIndexRegistry>().Registrations);
    }
    // Verifies that invalid provider configuration fails before any database operation is attempted.
    [Fact]
    public void AddRuniqRagPostgreSql_WithoutConnectionString_FailsFast()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddRuniqRagPostgreSql(_ => { }));
    }

    // Verifies that PostgreSQL replaces the default store and exposes structured health validation.
    [Fact]
    public void AddRuniqRagPostgreSql_RegistersProviderAndHealthCheck()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag();
        services.AddRuniqRagPostgreSql(options => options.ConnectionString = "Host=localhost;Database=runiq;Username=runiq;Password=runiq");
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IRagVectorStore>());
        Assert.NotNull(provider.GetRequiredService<IPostgreSqlRagHealthCheck>());
        Assert.NotNull(provider.GetRequiredService<IPostgreSqlRagDocumentStore>());
    }

    // Verifies a named PostgreSQL selection resolves to the effective runtime store without opening a connection.
    [Fact]
    public void AddRuniqRagPostgreSql_WithName_ResolvesNamedRuntimeStore()
    {
        var services = new ServiceCollection();
        services.AddRuniqRag(rag => rag.AddIndex("documents", index => index.UseDirectory("documents").UseEmbeddingModel("openai/test").UsePostgreSqlVectorStore("corporate")));
        services.AddRagEmbeddingClient("openai/test", _ => new TestEmbeddingClient());
        services.AddRuniqRagPostgreSql("corporate", options => options.ConnectionString = "Host=localhost;Database=runiq;Username=runiq;Password=runiq");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var runtime = scope.ServiceProvider.GetRequiredService<IRagIndexRuntimeConfigurationResolver>().Resolve("documents");

        Assert.NotNull(runtime.VectorStore);
        Assert.Equal("test", runtime.EmbeddingModel.ModelName);
    }

    // Verifies that duplicate registration deterministically selects the last PostgreSQL configuration.
    [Fact]
    public void AddRuniqRagPostgreSql_WhenRepeated_KeepsSingleActiveVectorStore()
    {
        var services = new ServiceCollection();
        services.AddRuniqRagPostgreSql(options => options.ConnectionString = "Host=first;Database=runiq;Username=runiq;Password=runiq");
        services.AddRuniqRagPostgreSql(options => options.ConnectionString = "Host=second;Database=runiq;Username=runiq;Password=runiq");
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRagVectorStore));
    }

    // Verifies that unsafe schema identifiers are rejected before SQL can be composed.
    [Fact]
    public void AddRuniqRagPostgreSql_WithUnsafeSchema_FailsFast()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddRuniqRagPostgreSql(options =>
        {
            options.ConnectionString = "Host=localhost;Database=runiq;Username=runiq;Password=runiq";
            options.Schema = "runiq;drop schema public";
        }));
    }

    private sealed class TestEmbeddingClient : IEmbeddingClient
    {
        public Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmbeddingResponse([new EmbeddingResult(0, [1f], 1)]));
    }
}
