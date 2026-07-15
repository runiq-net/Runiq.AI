using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Runiq.AI.Core.Rag;

/// <summary>
/// Maps dashboard API endpoints that expose read-only RAG configuration information.
/// </summary>
public static class RuniqRagApiEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the dashboard RAG information endpoint under the supplied base path.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="basePath">The dashboard API base path.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapRuniqRagApi(
        this IEndpointRouteBuilder endpoints,
        string basePath)
    {
        var normalizedBasePath = basePath.TrimEnd('/');

        endpoints.MapGet($"{normalizedBasePath}/rag", async (
            IRuniqRagInfoProvider infoProvider,
            CancellationToken cancellationToken) =>
        {
            var info = await infoProvider.GetInfoAsync(cancellationToken);

            return Results.Ok(info);
        });

        return endpoints;
    }
}

