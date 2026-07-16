namespace Runiq.AI.Rag.PostgreSql;

/// <summary>Describes PostgreSQL RAG provider readiness.</summary>
public sealed class PostgreSqlRagHealth
{
    /// <summary>Gets a value indicating whether all provider checks succeeded.</summary>
    public bool IsHealthy { get; init; }
    /// <summary>Gets a value indicating whether PostgreSQL was reachable.</summary>
    public bool CanConnect { get; init; }
    /// <summary>Gets a value indicating whether pgvector is installed.</summary>
    public bool VectorExtensionAvailable { get; init; }
    /// <summary>Gets a value indicating whether the provider schema exists.</summary>
    public bool SchemaAvailable { get; init; }
    /// <summary>Gets the installed migration version, or null when unavailable.</summary>
    public int? MigrationVersion { get; init; }
    /// <summary>Gets a diagnostic message.</summary>
    public string Diagnostic { get; init; } = string.Empty;
}

/// <summary>Validates PostgreSQL RAG provider readiness.</summary>
public interface IPostgreSqlRagHealthCheck
{
    /// <summary>Checks connection, extension, schema, migration, and index readability.</summary>
    /// <param name="cancellationToken">A token that cancels the check.</param>
    /// <returns>The structured provider health result.</returns>
    Task<PostgreSqlRagHealth> CheckAsync(CancellationToken cancellationToken = default);
}
