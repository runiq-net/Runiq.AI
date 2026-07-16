using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector.Npgsql;
using Runiq.AI.Rag.DependencyInjection;
using Runiq.AI.Rag.PostgreSql.Documents;

namespace Runiq.AI.Rag.PostgreSql.DependencyInjection;

/// <summary>Provides PostgreSQL RAG provider registrations.</summary>
public static class PostgreSqlRagServiceCollectionExtensions
{
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
