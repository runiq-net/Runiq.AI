using Microsoft.Extensions.DependencyInjection;
using Runiq.Mcp.Options;

namespace Runiq.Mcp.DependencyInjection;

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

        services
            .AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .WithToolsFromAssembly();

        return services;
    }
}