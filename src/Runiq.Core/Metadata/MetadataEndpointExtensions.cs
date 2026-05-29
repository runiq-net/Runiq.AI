using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Runiq.Core.Metadata;

/// <summary>
/// Runiq runtime metadata endpoint'lerini ASP.NET Core uygulamasÄ±na ekler.
/// </summary>
public static class MetadataEndpointExtensions
{
    /// <summary>
    /// Studio tarafÄ±ndan kullanÄ±lan runtime metadata endpoint'lerini map eder.
    /// </summary>
    public static IEndpointRouteBuilder MapRuniqMetadataApi(
        this IEndpointRouteBuilder endpoints,
        string pathPrefix = "/runiq/metadata")
    {
        var group = endpoints.MapGroup(pathPrefix);

        group.MapGet("/agents", (IRuntimeMetadataService metadataService) =>
        {
            return Results.Ok(metadataService.GetAgents());
        });

        group.MapGet("/tools", (IRuntimeMetadataService metadataService) =>
        {
            return Results.Ok(metadataService.GetTools());
        });

        group.MapGet("/context-spaces", (IRuntimeMetadataService metadataService) =>
        {
            return Results.Ok(metadataService.GetContextSpaces());
        });

        return endpoints;
    }
}
