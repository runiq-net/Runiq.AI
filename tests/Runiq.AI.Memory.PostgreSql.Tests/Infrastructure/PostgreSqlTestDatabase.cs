using Npgsql;
using Runiq.AI.Memory.PostgreSql.Configuration;
using Runiq.AI.Memory.PostgreSql.Migrations;
using Runiq.AI.Memory.PostgreSql.Persistence;

namespace Runiq.AI.Memory.PostgreSql.Tests.Infrastructure;

internal sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    internal static string ConnectionString => Environment.GetEnvironmentVariable("RUNIQ_MEMORY_TEST_CONNECTION") ??
        "Host=localhost;Port=54329;Database=runiq_rag_dev;Username=runiq_dev;Password=runiq_dev_only;Timeout=5";
    internal string Schema { get; } = "memory_test_" + Guid.NewGuid().ToString("N");
    internal MemoryDatabase Database { get; }
    internal PostgreSqlMemoryMigrator Migrator => new(Database);
    internal PostgreSqlMemoryOptions Options => new() { ConnectionString = ConnectionString, Schema = Schema };
    internal PostgreSqlTestDatabase() => Database = new(Options);

    internal async Task<object?> ExecuteAsync(string sql)
    {
        await using var connection = await Database.DataSource.OpenConnectionAsync();
        await using var command = Database.Command(connection, null, sql);
        return await command.ExecuteScalarAsync();
    }

    internal static async Task<string> InitialSqlAsync()
    {
        var assembly = typeof(PostgreSqlMemoryMigrator).Assembly;
        using var reader = new StreamReader(assembly.GetManifestResourceStream(
            assembly.GetManifestResourceNames().Single(n => n.EndsWith(".001_initial.sql")))!);
        return await reader.ReadToEndAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Only this fixture's generated schema is eligible for cleanup; never drop the shared database or volume.
            if (!Schema.StartsWith("memory_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected fixture schema.");
            await ExecuteAsync("DROP SCHEMA IF EXISTS __SCHEMA__ CASCADE");
        }
        finally { await Database.DisposeAsync(); }
    }
}
