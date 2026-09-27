using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Memory.Abstractions;

namespace Runiq.AI.Agents.Hosting.Memory;

/// <summary>Adapts authenticated HTTP host identity to transport-neutral Memory contracts.</summary>
public static class HttpMemoryServiceCollectionExtensions
{
    /// <summary>Registers scoped principal mapping without enabling any agent or selecting a store.</summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="options">Immutable claim and tenancy settings.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">Services or options are null.</exception>
    public static IServiceCollection AddRuniqHttpMemoryIdentity(this IServiceCollection services, HttpMemoryIdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddHttpContextAccessor();
        services.AddSingleton(options);
        services.AddScoped<IMemoryIdentityResolver, HttpMemoryIdentityResolver>();
        return services;
    }
}
