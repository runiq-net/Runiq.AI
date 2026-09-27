using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Memory.Services;

namespace Runiq.AI.Memory.DependencyInjection;

/// <summary>Registers neutral Memory foundations independently of agent opt-in and provider selection.</summary>
public static class MemoryServiceCollectionExtensions
{
    /// <summary>Registers scoped authorization; the host must supply identity, ownership, and access-policy adapters.</summary>
    /// <param name="services">The application's service collection.</param>
    /// <returns>The same service collection.</returns>
    /// <remarks>No store is selected and no I/O occurs. Required adapters are resolved only for enabled execution.</remarks>
    /// <exception cref="ArgumentNullException">The service collection is null.</exception>
    public static IServiceCollection AddRuniqMemory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<MemoryAuthorizationService>();
        return services;
    }
}
