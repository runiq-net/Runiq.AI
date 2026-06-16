using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Runiq.Core.Mcp;

/// <summary>
/// Maps dashboard API endpoints that expose read-only MCP server information.
/// </summary>
public static class RuniqMcpApiEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the dashboard MCP information endpoint under the supplied base path.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="basePath">The dashboard API base path.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapRuniqMcpApi(
        this IEndpointRouteBuilder endpoints,
        string basePath)
    {
        var normalizedBasePath = basePath.TrimEnd('/');

        endpoints.MapGet($"{normalizedBasePath}/mcp", (
            HttpRequest request,
            IEnumerable<EndpointDataSource> endpointDataSources) =>
        {
            var info = RuniqMcpInfoReader.Read(request, endpointDataSources);

            return Results.Ok(info);
        });

        endpoints.MapPost(
            $"{normalizedBasePath}/mcp/tools/{{toolName}}/run",
            async (
                string toolName,
                RuniqMcpToolRunRequest? request,
                RuniqMcpToolRunApiHandler handler,
                CancellationToken cancellationToken) =>
            {
                return await handler.RunAsync(
                    toolName,
                    request,
                    cancellationToken);
            });

        return endpoints;
    }
}
