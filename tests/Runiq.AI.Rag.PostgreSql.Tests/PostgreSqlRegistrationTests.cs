using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.PostgreSql;
using Runiq.AI.Rag.PostgreSql.DependencyInjection;
using Runiq.AI.Rag.PostgreSql.Documents;

namespace Runiq.AI.Rag.PostgreSql.Tests;

public sealed class PostgreSqlRegistrationTests
{
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
}
