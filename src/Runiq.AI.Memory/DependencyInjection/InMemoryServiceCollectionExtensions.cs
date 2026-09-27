using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Memory.Abstractions;
using Runiq.AI.Memory.Persistence.InMemory;

namespace Runiq.AI.Memory.DependencyInjection;

/// <summary>Provides explicit selection of volatile conversation persistence.</summary>
public static class InMemoryServiceCollectionExtensions
{
    /// <summary>Selects in-memory storage and its authoritative ownership lookup together.</summary>
    /// <param name="services">The host service collection; identity and policy remain host-owned.</param>
    /// <returns>The same collection. The last provider registration wins.</returns>
    /// <remarks>Data lives for one service-provider lifetime. Registration performs no I/O and does not enable agents.</remarks>
    public static IServiceCollection AddRuniqMemoryInMemory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddRuniqMemory();
        services.TryAddSingleton<InMemoryConversationState>();
        services.TryAddScoped<InMemoryConversationStore>();
        services.RemoveAll<IMemoryConversationStore>();
        services.RemoveAll<IMemoryOwnershipLookup>();
        services.AddScoped<IMemoryConversationStore>(sp => sp.GetRequiredService<InMemoryConversationStore>());
        services.AddScoped<IMemoryOwnershipLookup>(sp => sp.GetRequiredService<InMemoryConversationStore>());
        return services;
    }
}
