using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.DependencyInjection;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.PostgreSql.DependencyInjection;
using Runiq.AI.Memory.PostgreSql.Migrations;
using Runiq.AI.Memory.PostgreSql.Persistence;
using Runiq.AI.Memory.PostgreSql.Tests.Infrastructure;
using Runiq.AI.Rag.PostgreSql.DependencyInjection;
using Runiq.AI.Rag.PostgreSql;

namespace Runiq.AI.Memory.PostgreSql.Tests.DependencyInjection;

public sealed class PostgreSqlRegistrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Repeated provider selections replace store and ownership lookup together without opening an unavailable database.
    public void LastProviderWins_WithoutRegistrationIo(bool postgresLast)
    {
        var services = new ServiceCollection().AddScoped<IMemoryAccessPolicy, Policy>();
        void PostgreSql() => services.AddRuniqMemoryPostgreSql(o => o.ConnectionString = "Host=127.0.0.1;Port=1;Username=nobody;Timeout=1");
        PostgreSql(); PostgreSql();
        services.AddRuniqMemoryInMemory().AddRuniqMemoryInMemory();
        if (postgresLast) PostgreSql();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryConversationStore>();
        Assert.Same(store, scope.ServiceProvider.GetRequiredService<IMemoryOwnershipLookup>());
        Assert.Equal(postgresLast, store is PostgreSqlConversationStore);
        Assert.Single(scope.ServiceProvider.GetServices<IMemoryConversationStore>());
        Assert.Single(scope.ServiceProvider.GetServices<IMemoryOwnershipLookup>());
        Assert.NotNull(provider.GetRequiredService<PostgreSqlMemoryMigrator>());
    }

    [Fact]
    // Neutral registration alone supplies no implicit provider and invalid provider options fail at composition time.
    public void MissingAndInvalidConfiguration_FailClearly()
    {
        using var provider = new ServiceCollection().AddRuniqMemory().BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IMemoryConversationStore>());
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddRuniqMemoryPostgreSql(_ => { }));
        foreach (var schema in new[] { "public", "pg_catalog", "A", "x; DROP TABLE", new string('x', 64), "" })
            Assert.Throws<ArgumentException>(() => new ServiceCollection().AddRuniqMemoryPostgreSql(o =>
            { o.ConnectionString = PostgreSqlTestDatabase.ConnectionString; o.Schema = schema; }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    // Memory owns a distinct pool and never overwrites RAG options, data source, tables, or migration history.
    public async Task RagCoexistence_HasIndependentPoolsAndSchema(bool memoryLast)
    {
        await using var fixture = new PostgreSqlTestDatabase();
        var services = new ServiceCollection().AddScoped<IMemoryAccessPolicy, Policy>();
        void Memory() => services.AddRuniqMemoryPostgreSql(o =>
        { o.ConnectionString = PostgreSqlTestDatabase.ConnectionString; o.Schema = fixture.Schema; });
        void Rag() => services.AddRuniqRagPostgreSql(o =>
        { o.ConnectionString = "Host=127.0.0.1;Port=1;Username=rag_only"; o.Schema = "runiq_rag"; });
        if (memoryLast) { Rag(); Memory(); } else { Memory(); Rag(); }
        var provider = services.BuildServiceProvider();
        var memory = provider.GetRequiredService<MemoryDatabase>();
        Assert.NotSame(provider.GetRequiredService<NpgsqlDataSource>(), memory.DataSource);
        var before = await fixture.ExecuteAsync("SELECT string_agg(nspname,',' ORDER BY nspname) FROM pg_namespace WHERE nspname LIKE '%rag%'");
        await provider.GetRequiredService<PostgreSqlMemoryMigrator>().MigrateAsync();
        Assert.Equal(before, await fixture.ExecuteAsync("SELECT string_agg(nspname,',' ORDER BY nspname) FROM pg_namespace WHERE nspname LIKE '%rag%'"));
        await provider.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await memory.DataSource.OpenConnectionAsync());
    }

    [Fact]
    // Applying Memory migrations in a shared database leaves actual RAG documents and migration history unchanged.
    public async Task SharedDatabase_PreservesRagDocumentsAndMigrationHistory()
    {
        await using var memoryFixture = new PostgreSqlTestDatabase();
        await using var ragFixture = new PostgreSqlTestDatabase();
        await using var host = new ServiceCollection().AddScoped<IMemoryAccessPolicy, Policy>()
            .AddRuniqRagPostgreSql(o =>
            {
                o.ConnectionString = PostgreSqlTestDatabase.ConnectionString;
                o.Schema = ragFixture.Schema; o.InitializeSchema = true; o.CreateVectorExtension = true;
            })
            .AddRuniqMemoryPostgreSql(o =>
            { o.ConnectionString = PostgreSqlTestDatabase.ConnectionString; o.Schema = memoryFixture.Schema; })
            .BuildServiceProvider();
        var health = await host.GetRequiredService<IPostgreSqlRagHealthCheck>().CheckAsync();
        Assert.True(health.IsHealthy, health.Diagnostic);
        await ragFixture.ExecuteAsync("""
            INSERT INTO __SCHEMA__.rag_indexes(index_name,embedding_dimension,metric) VALUES ('index',3,'cosine');
            INSERT INTO __SCHEMA__.rag_documents(index_name,document_id,content_hash) VALUES ('index','sentinel','unchanged');
            """);
        var history = await ragFixture.ExecuteAsync("SELECT json_agg(t ORDER BY version)::text FROM __SCHEMA__.schema_migrations t");
        await host.GetRequiredService<PostgreSqlMemoryMigrator>().MigrateAsync();
        Assert.Equal(history, await ragFixture.ExecuteAsync("SELECT json_agg(t ORDER BY version)::text FROM __SCHEMA__.schema_migrations t"));
        Assert.Equal("unchanged", await ragFixture.ExecuteAsync("SELECT content_hash FROM __SCHEMA__.rag_documents WHERE document_id='sentinel'"));
        Assert.Equal(1L, await memoryFixture.ExecuteAsync("SELECT count(*) FROM __SCHEMA__.migration_history"));
    }

    private sealed class Policy : IMemoryAccessPolicy
    {
        public ValueTask<bool> CanAccessResourceAsync(MemoryIdentity identity, string resourceId, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }
}
