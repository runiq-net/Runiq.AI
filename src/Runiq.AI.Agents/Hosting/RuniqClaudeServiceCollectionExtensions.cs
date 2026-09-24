using System.ComponentModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Runiq.AI.Agents.Runtime.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Runtime.Claude;

namespace Runiq.AI.Core;

/// <summary>Provides advanced explicit registration for local Claude execution; normal agent hosts register automatically.</summary>
public static class RuniqClaudeServiceCollectionExtensions
{
    /// <summary>Registers Claude alongside existing executors without starting a process or changing authentication.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configure">Configures the trusted workspace and process limits.</param>
    /// <returns>The same collection for further host configuration.</returns>
    /// <remarks>Normal AddRuniqServer agent registration enables this executor automatically. This advanced entry point remains for compatibility and hosts without registered agent definitions. Custom executors override this fallback regardless of registration order. Multiple custom executors of the same kind remain configuration errors.</remarks>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static IServiceCollection AddRuniqClaudeExecutor(this IServiceCollection services,
        Action<ClaudeExecutorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.Configure(configure);
        return services.RegisterClaudeExecutor();
    }

    internal static IServiceCollection RegisterClaudeExecutor(this IServiceCollection services)
    {
        services.AddOptions<ClaudeExecutorOptions>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<ClaudeExecutorOptions>, ClaudeWorkingDirectoryDefaults>());
        services.TryAddSingleton<ICliProcessFactory, CliProcessFactory>();
        services.TryAddSingleton<ClaudeSessionGate>();
        services.TryAddScoped<ClaudeAgentExecutor>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentExecutor, FallbackAgentExecutor<ClaudeAgentExecutor>>());
        return services;
    }
}

// Apply host defaults after user configuration so explicit paths win regardless of registration order.
internal sealed class ClaudeWorkingDirectoryDefaults(IHostEnvironment? environment = null)
    : IPostConfigureOptions<ClaudeExecutorOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, ClaudeExecutorOptions options)
    {
        if (options.WorkingDirectory == string.Empty)
            options.WorkingDirectory = environment?.ContentRootPath ?? Directory.GetCurrentDirectory();
    }
}
