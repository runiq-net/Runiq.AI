using System.Text.RegularExpressions;
using Npgsql;

namespace Runiq.AI.Memory.PostgreSql.Configuration;

/// <summary>Configures the explicitly selected Memory database independently of RAG configuration.</summary>
public sealed class PostgreSqlMemoryOptions
{
    /// <summary>Gets or sets the required PostgreSQL connection string. Never log this value.</summary>
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>Gets or sets the Memory-owned schema, defaulting to runiq_memory.</summary>
    public string Schema { get; set; } = "runiq_memory";

    internal PostgreSqlMemoryOptions Snapshot()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) throw new ArgumentException("A Memory PostgreSQL connection string is required.");
        _ = new NpgsqlConnectionStringBuilder(ConnectionString);
        if (Schema is null || !Regex.IsMatch(Schema, "\\A[a-z_][a-z0-9_]{0,62}\\z", RegexOptions.CultureInvariant) ||
            Schema.StartsWith("pg_", StringComparison.Ordinal) || Schema is "public" or "information_schema")
            throw new ArgumentException("Use a dedicated lowercase ASCII Memory schema of 1-63 characters, starting with a letter or underscore.");
        return new() { ConnectionString = ConnectionString, Schema = Schema };
    }
}
