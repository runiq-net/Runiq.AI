using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Reflection;
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

    public async Task<IReadOnlyList<string>> QueryStringsAsync(
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            sql.Replace("__SCHEMA__", $"\"{Schema}\"", StringComparison.Ordinal),
            connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values;
    }

    public async Task<PostgreSqlRagHealth> ReinitializeSchemaAsync()
    {
        await using var secondProvider = CreateProvider(Schema);
        return await secondProvider.GetRequiredService<IPostgreSqlRagHealthCheck>().CheckAsync();
    }

    public async Task<PostgreSqlRagHealth> UpgradeLegacySchemaAsync()
    {
        var legacySchema = $"runiq_upgrade_{Guid.NewGuid():N}";
        try
        {
            var resource = typeof(PostgreSqlRagOptions).Assembly.GetManifestResourceNames()
                .Single(name => name.EndsWith("001_initial.sql", StringComparison.Ordinal));
            await using var stream = typeof(PostgreSqlRagOptions).Assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            var quotedSchema = $"\"{legacySchema}\"";
            var sql = (await reader.ReadToEndAsync()).Replace("__SCHEMA__", quotedSchema, StringComparison.Ordinal);
            await using (var connection = await DataSource.OpenConnectionAsync())
            {
                await using var createSchema = new NpgsqlCommand($"CREATE SCHEMA {quotedSchema}", connection);
                await createSchema.ExecuteNonQueryAsync();
                await using var migration = new NpgsqlCommand(sql, connection);
                await migration.ExecuteNonQueryAsync();
            }

            await using var upgradeProvider = CreateProvider(legacySchema);
            return await upgradeProvider.GetRequiredService<IPostgreSqlRagHealthCheck>().CheckAsync();
        }
        finally
        {
            await using var connection = await DataSource.OpenConnectionAsync();
            await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{legacySchema}\" CASCADE", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static ServiceProvider CreateProvider(string schema)
    {
        var services = new ServiceCollection();
        services.AddRuniqRagPostgreSql(options =>
        {
            options.ConnectionString = ConnectionString;
            options.Schema = schema;
            options.InitializeSchema = true;
            options.CreateVectorExtension = true;
        });
        return services.BuildServiceProvider();
    }
}
