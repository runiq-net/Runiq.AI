using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Runiq.Core.Agents;

namespace Runiq.Core.Teams;

/// <summary>
/// Runiq agent team execution endpoint'lerini ASP.NET Core uygulamasına ekler.
/// </summary>
public static class TeamEndpointExtensions
{
    /// <summary>
    /// Dashboard tarafından kullanılan agent team execution endpoint'lerini map eder.
    /// </summary>
    public static IEndpointRouteBuilder MapRuniqTeamApi(
        this IEndpointRouteBuilder endpoints,
        string pathPrefix = "/runiq/api")
    {
        var group = endpoints.MapGroup(pathPrefix);

        group.MapPost("/teams/{teamId}/chat", async (
            string teamId,
            AgentChatRequest request,
            TeamChatApiHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            return await handler.ChatAsync(
                teamId,
                request,
                httpContext,
                cancellationToken);
        });

        return endpoints;
    }
}