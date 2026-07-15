using Microsoft.AspNetCore.Routing;
using Runiq.AI.Core.Studio;
using Runiq.AI.Core.Workflows;

namespace Runiq.AI.Workflows.Hosting;

/// <summary>
/// Maps workflow dashboard APIs without requiring Core to reference the Workflows package.
/// </summary>
internal sealed class RuniqWorkflowDashboardEndpointContributor : IRuniqDashboardEndpointContributor
{
    /// <summary>
    /// Maps workflow metadata and execution endpoints under the active dashboard base path.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder used by the host application.</param>
    /// <param name="basePath">The normalized dashboard base path, without a trailing slash.</param>
    public void MapEndpoints(IEndpointRouteBuilder endpoints, string basePath)
    {
        endpoints.MapRuniqWorkflowApi($"{basePath}/api");
    }
}
