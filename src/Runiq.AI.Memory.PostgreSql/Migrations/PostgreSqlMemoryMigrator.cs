using System.Security.Cryptography;
using System.Text;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.PostgreSql.Persistence;

namespace Runiq.AI.Memory.PostgreSql.Migrations;

/// <summary>Applies embedded Memory migrations only when explicitly invoked by the host.</summary>
public sealed class PostgreSqlMemoryMigrator
{
    private readonly MemoryDatabase database;
    internal PostgreSqlMemoryMigrator(MemoryDatabase database) => this.database = database;

    /// <summary>Coordinates initialization across hosts using a transaction-scoped PostgreSQL advisory lock.</summary>
    /// <param name="cancellationToken">Cancels database work before commit; an uncertain outcome is safe to retry.</param>
    /// <returns>A task completed after the compatible schema is ready.</returns>
    /// <exception cref="MemoryStoreException">Schema history is incompatible or database migration fails.</exception>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var assembly = typeof(PostgreSqlMemoryMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".001_initial.sql", StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        await ApplyAsync([await reader.ReadToEndAsync(cancellationToken)], cancellationToken);
    }

    // Test fixtures may append a migration to exercise upgrades and rollback without inventing a shipped version.
    internal async Task ApplyAsync(IReadOnlyList<string> migrations, CancellationToken cancellationToken = default)
    {
        await MemoryDatabase.ExecuteAsync(async () =>
        {
            await using var connection = await database.DataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var gate = database.Command(connection, transaction,
                "SELECT pg_advisory_xact_lock(hashtextextended(@schema, 200))", ("schema", database.Schema)))
                await gate.ExecuteNonQueryAsync(cancellationToken);
            await using (var create = database.Command(connection, transaction, """
                CREATE SCHEMA IF NOT EXISTS __SCHEMA__;
                CREATE TABLE IF NOT EXISTS __SCHEMA__.migration_history (
                    version integer PRIMARY KEY CHECK (version > 0),
                    checksum text NOT NULL,
                    applied_at timestamptz NOT NULL DEFAULT now()
                );
                """)) await create.ExecuteNonQueryAsync(cancellationToken);
            var applied = new List<(int Version, string Checksum)>();
            await using (var query = database.Command(connection, transaction, "SELECT version, checksum FROM __SCHEMA__.migration_history ORDER BY version"))
            await using (var rows = await query.ExecuteReaderAsync(cancellationToken))
                while (await rows.ReadAsync(cancellationToken)) applied.Add((rows.GetInt32(0), rows.GetString(1)));
            if (applied.Count > migrations.Count || applied.Where((m, i) => m.Version != i + 1 || m.Checksum != Checksum(migrations[i])).Any())
                throw new MemoryStoreException(MemoryStoreError.IncompatibleSchema);
            for (var index = applied.Count; index < migrations.Count; index++)
            {
                await using (var migration = database.Command(connection, transaction, migrations[index]))
                    await migration.ExecuteNonQueryAsync(cancellationToken);
                await using var record = database.Command(connection, transaction,
                    "INSERT INTO __SCHEMA__.migration_history(version, checksum) VALUES (@version, @checksum)",
                    ("version", index + 1), ("checksum", Checksum(migrations[index])));
                await record.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    private static string Checksum(string sql) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql.Replace("\r\n", "\n", StringComparison.Ordinal))));
}
