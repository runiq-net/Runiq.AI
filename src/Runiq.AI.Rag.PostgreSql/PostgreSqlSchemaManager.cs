using System.Reflection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Runiq.AI.Rag.PostgreSql;

internal sealed class PostgreSqlSchemaManager
{
    internal const int CurrentVersion = 2;
    private readonly NpgsqlDataSource dataSource;
    private readonly PostgreSqlRagOptions options;
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool initialized;

    public PostgreSqlSchemaManager(NpgsqlDataSource dataSource, IOptions<PostgreSqlRagOptions> options)
    {
        this.dataSource = dataSource;
        this.options = options.Value;
    }

    public string QuotedSchema => new NpgsqlCommandBuilder().QuoteIdentifier(options.Schema);

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!options.InitializeSchema || initialized) return;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (initialized) return;
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (options.CreateVectorExtension)
            {
                await using var extension = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection);
                await extension.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await using var check = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_extension WHERE extname='vector')", connection);
                var exists = (bool)(await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
                if (!exists)
                    throw new InvalidOperationException("The PostgreSQL vector extension is required. Install it or explicitly enable CreateVectorExtension.");
            }
            await connection.ReloadTypesAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (var createSchema = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS {QuotedSchema}", connection, transaction))
                await createSchema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(name => name.Contains(".Migrations.", StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal);
            foreach (var resource in resources)
            {
                await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)!;
                using var reader = new StreamReader(stream);
                var sql = (await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false))
                    .Replace("__SCHEMA__", QuotedSchema, StringComparison.Ordinal);
                await using var migration = new NpgsqlCommand(sql, connection, transaction);
                await migration.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            initialized = true;
        }
        finally { gate.Release(); }
    }
}
