using Microsoft.Extensions.DependencyInjection;
using Runiq.Workflows.Infrastructure;
using Runiq.Workflows.Interfaces;
using Runiq.Workflows.Services;

namespace Runiq.Workflows;

/// <summary>
/// Registers Runiq flow services in the dependency injection container.
/// </summary>
public static class RuniqWorkflowsServiceCollectionExtensions
{
    public static IServiceCollection AddRuniqWorkflows(this IServiceCollection services)
    {
        return services.AddRuniqWorkflows(_ => { });
    }

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

        return services;
    }
}
