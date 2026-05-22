using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Runiq.Core.Agents;

/// <summary>
/// Runiq agent execution endpoint'lerini ASP.NET Core uygulamasına ekler.
/// </summary>
public static class AgentEndpointExtensions
{
    /// <summary>
    /// Studio tarafından kullanılan agent execution endpoint'lerini map eder.
    /// </summary>
    public static IEndpointRouteBuilder MapRuniqAgentApi(
        this IEndpointRouteBuilder endpoints,
        string pathPrefix = "/runiq/api")
    {
        var group = endpoints.MapGroup(pathPrefix);

        group.MapPost("/agents/{agentId}/chat", async (
            string agentId,
            AgentChatRequest request,
            AgentChatApiHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            return await handler.ChatAsync(
                agentId,
                request,
                httpContext,
                cancellationToken);
        });

        return endpoints;
    }
}