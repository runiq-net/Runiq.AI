using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.PostgreSql;
using Runiq.AI.Rag.PostgreSql.DependencyInjection;
using Runiq.AI.Rag.PostgreSql.Documents;

namespace Runiq.AI.Rag.PostgreSql.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlIntegrationCollection : ICollectionFixture<PostgreSqlIntegrationFixture>
{
    public const string Name = "PostgreSQL integration";
}

public sealed class PostgreSqlIntegrationFixture : IAsyncLifetime
{
    private const string ConnectionString = "Host=localhost;Port=54329;Database=runiq_rag_dev;Username=runiq_dev;Password=runiq_dev_only;Timeout=5";
    private ServiceProvider? provider;

    public string Schema { get; } = $"runiq_test_{Guid.NewGuid():N}";
    public IRagVectorStore VectorStore => provider!.GetRequiredService<IRagVectorStore>();
    public IPostgreSqlRagDocumentStore Documents => provider!.GetRequiredService<IPostgreSqlRagDocumentStore>();
    public IPostgreSqlRagHealthCheck Health => provider!.GetRequiredService<IPostgreSqlRagHealthCheck>();
    public NpgsqlDataSource DataSource => provider!.GetRequiredService<NpgsqlDataSource>();

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddRuniqRagPostgreSql(options =>
        {
            options.ConnectionString = ConnectionString;
            options.Schema = Schema;
            options.InitializeSchema = true;
            options.CreateVectorExtension = true;
        });
        provider = services.BuildServiceProvider();
        var health = await Health.CheckAsync();
        if (!health.IsHealthy) throw new InvalidOperationException(health.Diagnostic);
    }

    public async Task DisposeAsync()
    {
        if (provider is null) return;
        await using (var connection = await DataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{Schema}\" CASCADE", connection))
            await command.ExecuteNonQueryAsync();
        await provider.DisposeAsync();
    }

    public async Task<long> ScalarLongAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql.Replace("__SCHEMA__", $"\"{Schema}\"", StringComparison.Ordinal), connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public async Task<string?> ScalarStringAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql.Replace("__SCHEMA__", $"\"{Schema}\"", StringComparison.Ordinal), connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return await command.ExecuteScalarAsync() as string;
    }
}
