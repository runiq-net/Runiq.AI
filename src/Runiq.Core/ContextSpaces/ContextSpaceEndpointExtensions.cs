using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Runiq.Core.ContextSpaces;

/// <summary>
/// Runiq Dashboard context space read-only API endpoint'lerini map eder.
/// </summary>
public static class ContextSpaceEndpointExtensions
{
    /// <summary>
    /// Dashboard context space API endpoint'lerini verilen path prefix altında map eder.
    /// </summary>
    public static IEndpointRouteBuilder MapRuniqContextSpaceApi(
        this IEndpointRouteBuilder endpoints,
        string pathPrefix = "/runiq/api")
    {
        var group = endpoints.MapGroup(pathPrefix);

        group.MapGet(
            "/context-spaces/{contextSpaceId}/source-documents",
            async (
                string contextSpaceId,
                ContextSpaceSourceDocumentApiHandler handler,
                CancellationToken cancellationToken) =>
            {
                return await handler.ListAsync(
                    contextSpaceId,
                    cancellationToken);
            });

        group.MapGet(
            "/context-spaces/{contextSpaceId}/source-documents/preview",
            async (
                string contextSpaceId,
                string? sourceId,
                string? path,
                ContextSpaceSourceDocumentApiHandler handler,
                CancellationToken cancellationToken) =>
            {
                return await handler.PreviewAsync(
                    contextSpaceId,
                    sourceId,
                    path,
                    cancellationToken);
            });

        group.MapGet(
            "/context-spaces/{contextSpaceId}/skill-documents",
            (
                string contextSpaceId,
                ContextSpaceSkillDocumentApiHandler handler) =>
            {
                return handler.List(contextSpaceId);
            });

        group.MapGet(
            "/context-spaces/{contextSpaceId}/skill-documents/preview",
            async (
                string contextSpaceId,
                string? skillId,
                ContextSpaceSkillDocumentApiHandler handler,
                CancellationToken cancellationToken) =>
            {
                return await handler.PreviewAsync(
                    contextSpaceId,
                    skillId,
                    cancellationToken);
            });

        return endpoints;
    }
}
