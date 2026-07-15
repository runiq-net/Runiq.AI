using Microsoft.AspNetCore.Routing;
using Runiq.AI.Core.Agents;
using Runiq.AI.Core.Metadata;
using Runiq.AI.Core.Studio;
using Runiq.AI.Core.Tools;

namespace Runiq.AI.Core;

/// <summary>
/// Maps the agent-owned dashboard APIs while keeping the Core dashboard host independent of agent runtime types.
/// </summary>
internal sealed class RuniqAgentDashboardEndpointContributor : IRuniqDashboardEndpointContributor
{
    /// <summary>
    /// Maps agent metadata, chat, and tool playground endpoints under the active dashboard base path.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder used by the host application.</param>
    /// <param name="basePath">The normalized dashboard base path, without a trailing slash.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints, string basePath)
    {
        endpoints.MapRuniqMetadataApi($"{basePath}/metadata");
        endpoints.MapRuniqAgentApi($"{basePath}/api");
        endpoints.MapRuniqToolApi($"{basePath}/api");
    }
}
