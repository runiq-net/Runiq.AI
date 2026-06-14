using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Runiq.Mcp.DependencyInjection;

public static class RuniqMcpToolServiceCollectionExtensions
{
    public static IServiceCollection AddMcpTool<TTool>(
        this IServiceCollection services)
        where TTool : class
    {
        services.AddTransient<TTool>();

        services
            .AddMcpServer()
            .WithTools<TTool>();

        return services;
    }
}