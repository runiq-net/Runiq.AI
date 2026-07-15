using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Runiq.AI.Mcp;

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
