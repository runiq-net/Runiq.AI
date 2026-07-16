namespace Runiq.AI.Rag.PostgreSql;

/// <summary>Configures the PostgreSQL and pgvector RAG provider.</summary>
public sealed class PostgreSqlRagOptions
{
    /// <summary>Gets or sets the PostgreSQL connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets the database schema owned by the provider.</summary>
    public string Schema { get; set; } = "runiq_rag";

    /// <summary>Gets or sets a value indicating whether pending provider migrations are applied at startup.</summary>
    public bool InitializeSchema { get; set; }

    /// <summary>Gets or sets a value indicating whether initialization may create the vector extension.</summary>
    public bool CreateVectorExtension { get; set; }
}
