using Microsoft.AspNetCore.Routing;
using Runiq.AI.Core.Studio;

namespace Runiq.AI.Rag.Hosting;

internal sealed class RuniqRagDashboardEndpointContributor : IRuniqDashboardEndpointContributor
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints, string basePath) =>
        endpoints.MapRuniqRagManagementApi($"{basePath}/api");
}
