using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Runiq.Mcp.Routing;

public static class RuniqMcpEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapRuniqMcp(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/mcp")
    {
        endpoints.MapMcp(pattern);

        return endpoints;
    }
}