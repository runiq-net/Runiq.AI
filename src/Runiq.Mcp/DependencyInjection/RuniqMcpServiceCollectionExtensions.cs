using System.Reflection;
using Microsoft.Extensions.DependencyInjection;


namespace Runiq.Mcp;

public static class RuniqMcpServiceCollectionExtensions
{
    public static IServiceCollection AddRuniqMcp(
        this IServiceCollection services,
        Action<RuniqMcpOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<RuniqMcpOptions>(_ => { });
        }

        var serverBuilder = services
            .AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            });

        var entryAssembly = Assembly.GetEntryAssembly();

        if (entryAssembly is not null)
        {
            serverBuilder.WithToolsFromAssembly(entryAssembly);
        }
        else
        {
            serverBuilder.WithToolsFromAssembly();
        }

        return services;
    }
}