using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector.Npgsql;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.Configuration;
using Runiq.AI.Rag.Abstractions.VectorStores;
using Runiq.AI.Rag.PostgreSql.Documents;

namespace Runiq.AI.Rag.PostgreSql.DependencyInjection;

/// <summary>Provides PostgreSQL RAG provider registrations.</summary>
public static class PostgreSqlRagServiceCollectionExtensions
{
    /// <summary>Selects the default PostgreSQL/pgvector store reference without opening a connection.</summary>
    /// <param name="builder">The index builder.</param>
    /// <returns>The same index builder.</returns>
    public static RagIndexBuilder UsePostgreSqlVectorStore(this RagIndexBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseVectorStore(new RagVectorStoreReference("postgresql", "PostgreSql", "PostgreSQL/pgvector"));
    }

    /// <summary>Selects a named PostgreSQL/pgvector store reference without opening a connection.</summary>
    /// <param name="builder">The index builder.</param>
    /// <param name="name">The non-empty named store registration.</param>
    /// <returns>The same index builder.</returns>
    public static RagIndexBuilder UsePostgreSqlVectorStore(this RagIndexBuilder builder, string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A non-empty PostgreSQL store name is required.", nameof(name));
        var normalized = name.Trim();
        return builder.UseVectorStore(new RagVectorStoreReference($"postgresql/{normalized}", "PostgreSql", $"PostgreSQL/pgvector ({normalized})", normalized));
    }
    /// <summary>Adds PostgreSQL persistence and pgvector search as the active RAG vector store.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The provider configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRuniqRagPostgreSql(
        this IServiceCollection services,
        Action<PostgreSqlRagOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new PostgreSqlRagOptions();
        configure(options);
        Validate(options);
        services.AddOptions<PostgreSqlRagOptions>().Configure(configure);
        services.Replace(ServiceDescriptor.Singleton(sp =>
        {
            var configured = sp.GetRequiredService<IOptions<PostgreSqlRagOptions>>().Value;
            var builder = new NpgsqlDataSourceBuilder(configured.ConnectionString);
            builder.UseVector();
            return builder.Build();
        }));
        services.TryAddSingleton<PostgreSqlSchemaManager>();
        services.TryAddSingleton<IPostgreSqlRagHealthCheck, PostgreSqlRagHealthCheck>();
        services.TryAddSingleton<IPostgreSqlRagDocumentStore, PostgreSqlRagDocumentStore>();
        services.AddRagVectorStore<PostgreSqlRagVectorStore>();
        services.AddRagVectorStore("postgresql", provider => provider.GetRequiredService<IRagVectorStore>());
        return services;
    }

    /// <summary>Adds PostgreSQL persistence and binds it to a named index vector-store reference.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The name used by <see cref="UsePostgreSqlVectorStore(RagIndexBuilder, string)"/>.</param>
    /// <param name="configure">The provider configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRuniqRagPostgreSql(this IServiceCollection services, string name, Action<PostgreSqlRagOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A non-empty PostgreSQL store name is required.", nameof(name));
        services.AddRuniqRagPostgreSql(configure);
        services.AddRagVectorStore($"postgresql/{name.Trim()}", provider => provider.GetRequiredService<IRagVectorStore>());
        return services;
    }

    private static void Validate(PostgreSqlRagOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException("A PostgreSQL connection string is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Schema) || !options.Schema.All(c => char.IsLetterOrDigit(c) || c == '_'))
            throw new ArgumentException("Schema must contain only letters, digits, or underscores.", nameof(options));
    }
}
