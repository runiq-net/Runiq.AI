using Microsoft.Extensions.DependencyInjection;

namespace Runiq.Workflows;

/// <summary>
/// Runiq workflow servislerini dependency injection container'a ekleyen extension metodlarını içerir.
/// </summary>
public static class RuniqWorkflowsServiceCollectionExtensions
{
    /// <summary>
    /// Workflow DSL ve execution runtime servislerini kaydeder.
    /// </summary>
    public static IServiceCollection AddRuniqWorkflows(this IServiceCollection services)
    {
        return services.AddRuniqWorkflows(_ => { });
    }

    /// <summary>
    /// Workflow DSL, kayıtlı workflow tanımları ve execution runtime servislerini kaydeder.
    /// </summary>
    public static IServiceCollection AddRuniqWorkflows(
        this IServiceCollection services,
        Action<WorkflowOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new WorkflowOptions();
        configure(options);

        var registry = new WorkflowRegistry();

        foreach (var workflow in options.Workflows)
        {
            registry.AddWorkflow(workflow);
        }

        services.AddSingleton(registry);
        services.AddSingleton<IWorkflowAgentResolver, WorkflowAgentResolver>();
        services.AddScoped<IWorkflowAgentExecutor, WorkflowAgentExecutor>();
        services.AddScoped<IWorkflowExecutionRuntime, WorkflowExecutionRuntime>();

        return services;
    }
}
