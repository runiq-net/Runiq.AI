using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.ContextSpaces.Services;
using Runiq.AI.Core.ContextSpaces;
using Runiq.AI.Core.Mcp;
using Runiq.AI.Core.Rag;

namespace Runiq.AI.Core;

/// <summary>
/// Registers reusable Runiq server services that do not depend on agent runtime types.
/// </summary>
public static class RuniqServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers Core dashboard and infrastructure services for non-agent runtime surfaces.
    /// </summary>
    /// <param name="services">The host application's service collection.</param>
    /// <returns>The same service collection for fluent startup composition.</returns>
    public static IServiceCollection AddRuniqServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IContextSpaceSkillDiscoveryService, ContextSpaceSkillDiscoveryService>();
        services.AddSingleton<IContextSpaceSourceReader, ContextSpaceFileSystemSourceReader>();

        services.AddScoped<RuniqMcpToolRunApiHandler>();

        services.AddScoped<ContextSpaceSourceDocumentApiHandler>();
        services.AddScoped<ContextSpaceSkillDocumentApiHandler>();

        services.TryAddScoped<IRuniqRagInfoProvider, RuniqRagInfoReader>();

        return services;
    }
}