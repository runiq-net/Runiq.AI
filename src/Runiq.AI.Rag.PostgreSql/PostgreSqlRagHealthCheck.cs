using Microsoft.Extensions.Options;
using Npgsql;

namespace Runiq.AI.Rag.PostgreSql;

internal sealed class PostgreSqlRagHealthCheck : IPostgreSqlRagHealthCheck
{
    private readonly NpgsqlDataSource dataSource;
    private readonly PostgreSqlSchemaManager schemaManager;
    private readonly PostgreSqlRagOptions options;

    public PostgreSqlRagHealthCheck(NpgsqlDataSource dataSource, PostgreSqlSchemaManager schemaManager, IOptions<PostgreSqlRagOptions> options)
    { this.dataSource = dataSource; this.schemaManager = schemaManager; this.options = options.Value; }

    public async Task<PostgreSqlRagHealth> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await schemaManager.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            const string sql = "SELECT EXISTS(SELECT 1 FROM pg_extension WHERE extname='vector'), EXISTS(SELECT 1 FROM information_schema.schemata WHERE schema_name=@schema)";
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("schema", options.Schema);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var vector = reader.GetBoolean(0); var schema = reader.GetBoolean(1);
            await reader.CloseAsync().ConfigureAwait(false);
            int? version = null;
            if (schema)
            {
                await using var versionCommand = new NpgsqlCommand($"SELECT max(version) FROM {schemaManager.QuotedSchema}.schema_migrations", connection);
                version = await versionCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as int?;
                await using var readable = new NpgsqlCommand($"SELECT count(*) FROM {schemaManager.QuotedSchema}.rag_indexes", connection);
                await readable.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
            var healthy = vector && schema && version == PostgreSqlSchemaManager.CurrentVersion;
            return new PostgreSqlRagHealth { IsHealthy = healthy, CanConnect = true, VectorExtensionAvailable = vector, SchemaAvailable = schema, MigrationVersion = version, Diagnostic = healthy ? "PostgreSQL RAG provider is ready." : "PostgreSQL RAG provider schema or migration is not ready." };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return new PostgreSqlRagHealth { Diagnostic = ex.Message }; }
    }
}
