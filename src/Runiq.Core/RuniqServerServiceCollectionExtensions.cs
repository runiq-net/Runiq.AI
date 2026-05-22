using Microsoft.Extensions.DependencyInjection;
using Runiq.Agents;
using Runiq.Agents.Providers.OpenAI;
using Runiq.Agents.Runtime;
using Runiq.Agents.Validation;
using Runiq.Core.Agents;
using Runiq.Core.Configuration;
using Runiq.Core.Metadata;

namespace Runiq.Core;

/// <summary>
/// Runiq server tarafı servis kayıtlarını host uygulamanın DI container'ına ekleyen extension metodlarını içerir.
/// </summary>
public static class RuniqServerServiceCollectionExtensions
{
    /// <summary>
    /// Runiq Dashboard ve runtime metadata için gerekli server servislerini kaydeder.
    /// </summary>
    public static IServiceCollection AddRuniqServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRuntimeMetadataService, RuntimeMetadataService>();

        services.AddHttpClient<OpenAIResponsesClient>();
        services.AddHttpClient<OpenAICompatibleClient>();

        services.AddScoped<AgentExecutionRuntime>();
        services.AddScoped<AgentChatApiHandler>();

        return services;
    }

    /// <summary>
    /// Runiq Dashboard, runtime metadata ve host uygulama tarafından tanımlanan agent kayıtlarını ekler.
    /// </summary>
    public static IServiceCollection AddRuniqServer(
        this IServiceCollection services,
        Action<RuniqServerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RuniqServerOptions();

        configure(options);

        AgentValidator.ValidateRegisteredAgents(options.Agents);

        foreach (var agent in options.Agents)
        {
            services.AddSingleton(agent);
        }

        services.AddRuniqServer();

        return services;
    }
}