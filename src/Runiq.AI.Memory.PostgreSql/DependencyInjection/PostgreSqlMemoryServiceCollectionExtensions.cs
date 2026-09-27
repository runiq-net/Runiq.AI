using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.DependencyInjection;
using Runiq.AI.Memory.PostgreSql.Configuration;
using Runiq.AI.Memory.PostgreSql.Migrations;
using Runiq.AI.Memory.PostgreSql.Persistence;

namespace Runiq.AI.Memory.PostgreSql.DependencyInjection;

/// <summary>Provides explicit PostgreSQL provider selection without connection or migration side effects.</summary>
public static class PostgreSqlMemoryServiceCollectionExtensions
{
    /// <summary>Selects the PostgreSQL store and ownership lookup together; the last provider selection wins.</summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configure">Supplies the required connection and optional dedicated schema.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>The container owns a Memory-specific singleton pool. Stores and host policies are scoped.
    /// Resolve PostgreSqlMemoryMigrator and explicitly invoke MigrateAsync to initialize storage.</remarks>
    /// <exception cref="ArgumentException">Connection or schema configuration is invalid.</exception>
    public static IServiceCollection AddRuniqMemoryPostgreSql(this IServiceCollection services, Action<PostgreSqlMemoryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new PostgreSqlMemoryOptions();
        configure(options);
        var snapshot = options.Snapshot();
        services.AddRuniqMemory();
        services.RemoveAll<MemoryDatabase>();
        services.AddSingleton(_ => new MemoryDatabase(snapshot));
        services.TryAddSingleton(sp => new PostgreSqlMemoryMigrator(sp.GetRequiredService<MemoryDatabase>()));
        services.TryAddScoped<PostgreSqlConversationStore>();
        services.RemoveAll<IMemoryConversationStore>();
        services.RemoveAll<IMemoryOwnershipLookup>();
        services.AddScoped<IMemoryConversationStore>(sp => sp.GetRequiredService<PostgreSqlConversationStore>());
        services.AddScoped<IMemoryOwnershipLookup>(sp => sp.GetRequiredService<PostgreSqlConversationStore>());
        return services;
    }
}
