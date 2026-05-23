using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Runiq.Core.Tools;

/// <summary>
/// Runiq Dashboard tool playground API endpoint'lerini map eder.
/// </summary>
public static class ToolEndpointExtensions
{
    /// <summary>
    /// Dashboard tool API endpoint'lerini verilen path prefix altında map eder.
    /// </summary>
    public static IEndpointRouteBuilder MapRuniqToolApi(
        this IEndpointRouteBuilder endpoints,
        string pathPrefix = "/runiq/api")
    {
        var group = endpoints.MapGroup(pathPrefix);

        group.MapPost(
            "/tools/{toolName}/run",
            async (
                string toolName,
                ToolRunRequest? request,
                ToolRunApiHandler handler,
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