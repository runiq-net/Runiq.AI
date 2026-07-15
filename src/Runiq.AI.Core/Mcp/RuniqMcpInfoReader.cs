using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Runiq.AI.Core.Mcp;

internal static class RuniqMcpInfoReader
{
    public static RuniqMcpInfo Read(
        HttpRequest request,
        IEnumerable<EndpointDataSource> endpointDataSources)
    {
        var endpoint = FindMcpEndpoint(endpointDataSources);
        var tools = DiscoverMcpTools();

        return new RuniqMcpInfo
        {
            Enabled = endpoint is not null,
            Endpoint = endpoint,
            FullUrl = endpoint is null ? null : BuildFullUrl(request, endpoint),
            Transport = "Streamable HTTP",
            Stateless = true,
            Authentication = "None",
            Tools = tools
        };
    }

    private static string? FindMcpEndpoint(IEnumerable<EndpointDataSource> endpointDataSources)
    {
        foreach (var endpoint in endpointDataSources.SelectMany(source => source.Endpoints))
        {
            if (endpoint is not RouteEndpoint routeEndpoint)
            {
                continue;
            }

            var route = routeEndpoint.RoutePattern.RawText;

            if (string.IsNullOrWhiteSpace(route))
            {
                continue;
            }

            if (IsMcpServerEndpoint(routeEndpoint))
            {
                return NormalizeEndpointPath(route);
            }
        }

        return null;
    }

    private static bool IsMcpServerEndpoint(RouteEndpoint endpoint)
    {
        if (endpoint.DisplayName?.Contains(
                "MCP Streamable HTTP",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return endpoint.Metadata.Any(metadata =>
        {
            var metadataText = metadata.ToString();
            var metadataTypeName = metadata.GetType().FullName;

            return ContainsModelContextProtocolName(metadataText) ||
                ContainsModelContextProtocolName(metadataTypeName);
        });
    }

    private static bool ContainsModelContextProtocolName(string? value)
    {
        return value?.Contains(
            "ModelContextProtocol",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string NormalizeEndpointPath(string endpoint)
    {
        var normalized = endpoint.Trim();

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized.TrimEnd('/');
    }

    private static string BuildFullUrl(HttpRequest request, string endpoint)
    {
        var pathBase = request.PathBase.Value?.TrimEnd('/') ?? string.Empty;

        return $"{request.Scheme}://{request.Host}{pathBase}{endpoint}";
    }

    private static IReadOnlyList<RuniqMcpToolInfo> DiscoverMcpTools()
    {
        return RuniqMcpToolCatalog
            .DiscoverTools()
            .Select(RuniqMcpToolCatalog.ToInfo)
            .ToArray();
    }
}

