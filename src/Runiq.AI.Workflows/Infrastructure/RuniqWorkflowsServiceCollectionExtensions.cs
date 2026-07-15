using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Runiq.AI.Core.Studio;
using Runiq.AI.Workflows.Hosting;
using Runiq.AI.Workflows.Infrastructure;
using Runiq.AI.Workflows.Interfaces;
using Runiq.AI.Workflows.Services;

namespace Runiq.AI.Workflows;

/// <summary>
/// Registers Runiq flow services in the dependency injection container.
/// </summary>
public static class RuniqWorkflowsServiceCollectionExtensions
{
    /// <summary>
    /// Registers workflow services with an empty flow catalog.
    /// </summary>
    /// <param name="services">The host application's service collection.</param>
    /// <returns>The same service collection for fluent startup composition.</returns>
    public static IServiceCollection AddRuniqWorkflows(this IServiceCollection services)
    {
        return services.AddRuniqWorkflows(_ => { });
    }

    /// <summary>
    /// Registers workflow orchestration services and dashboard endpoints for configured flows.
    /// </summary>
    /// <param name="services">The host application's service collection.</param>
    /// <param name="configure">A callback that adds flow definitions to the workflow options.</param>
    /// <returns>The same service collection for fluent startup composition.</returns>
    public static IServiceCollection AddRuniqWorkflows(
        this IServiceCollection services,
        Action<FlowOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new FlowOptions();
        configure(options);

        var catalog = new FlowCatalog();

        foreach (var flow in options.Flows)
        {
            catalog.AddFlow(flow);
        }

        services.AddSingleton(catalog);
        services.AddSingleton<IAgentStepResolver, RegisteredAgentStepResolver>();
        services.AddScoped<IAgentStepExecutor, RuniqAgentStepExecutor>();
        services.AddScoped<IFlowRunner, FlowRunner>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRuniqDashboardEndpointContributor, RuniqWorkflowDashboardEndpointContributor>());

        return services;
    }
}

