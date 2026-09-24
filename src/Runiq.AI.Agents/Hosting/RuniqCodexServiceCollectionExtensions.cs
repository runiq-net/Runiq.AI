using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Runtime.Codex;

namespace Runiq.AI.Core;

/// <summary>Provides opt-in registration of local, authenticated Codex CLI execution.</summary>
public static class RuniqCodexServiceCollectionExtensions
{
    /// <summary>Registers Codex alongside existing executors without starting a process or changing authentication.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configure">Configures the trusted workspace and process limits.</param>
    /// <returns>The same collection for further host configuration.</returns>
    /// <remarks>Also register the agent server. Duplicate executor kinds remain configuration errors.</remarks>
    public static IServiceCollection AddRuniqCodexExecutor(this IServiceCollection services,
        Action<CodexExecutorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.Configure(configure);
        services.TryAddSingleton<ICodexProcessFactory, CodexProcessFactory>();
        services.TryAddSingleton<CodexSessionGate>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentExecutor, CodexAgentExecutor>());
        return services;
    }
}
