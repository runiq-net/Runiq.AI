using Microsoft.AspNetCore.Routing;

namespace Runiq.AI.Core.Studio;

/// <summary>
/// Allows higher-level packages to attach their dashboard endpoints without introducing reverse project dependencies into Core.
/// </summary>
public interface IRuniqDashboardEndpointContributor
{
    /// <summary>
    /// Maps package-owned endpoints under the dashboard base path.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder used by the host application.</param>
    /// <param name="basePath">The normalized dashboard base path, without a trailing slash.</param>
    void MapEndpoints(IEndpointRouteBuilder endpoints, string basePath);
}
