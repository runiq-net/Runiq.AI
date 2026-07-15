using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Runiq.AI.Core.Mcp;

namespace Runiq.AI.Core.Tests.Dashboard;

public sealed class RuniqMcpInfoReaderTests
{
    [Fact]
    public void Read_reports_real_mcp_endpoint_not_dashboard_info_route()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "http";
        request.Host = new HostString("localhost:6185");

        var endpoints = new[]
        {
            CreateEndpoint("/dashboard/api/mcp", "HTTP: GET /dashboard/api/mcp"),
            CreateEndpoint("/mcp/", "MCP Streamable HTTP | HTTP: POST /mcp/")
        };

        var info = RuniqMcpInfoReader.Read(
            request,
            new[] { new DefaultEndpointDataSource(endpoints) });

        Assert.True(info.Enabled);
        Assert.Equal("/mcp", info.Endpoint);
        Assert.Equal("http://localhost:6185/mcp", info.FullUrl);
        Assert.Equal("Streamable HTTP", info.Transport);
        Assert.True(info.Stateless);
        Assert.Equal("None", info.Authentication);
    }

    [Fact]
    public void Read_reports_custom_mcp_endpoint()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "https";
        request.Host = new HostString("example.test");

        var endpoint = CreateEndpoint(
            "/ai/mcp/",
            "MCP Streamable HTTP | HTTP: POST /ai/mcp/");

        var info = RuniqMcpInfoReader.Read(
            request,
            new[] { new DefaultEndpointDataSource(endpoint) });

        Assert.True(info.Enabled);
        Assert.Equal("/ai/mcp", info.Endpoint);
        Assert.Equal("https://example.test/ai/mcp", info.FullUrl);
    }

    private static RouteEndpoint CreateEndpoint(string pattern, string displayName)
    {
        var builder = new RouteEndpointBuilder(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(pattern),
            order: 0)
        {
            DisplayName = displayName
        };

        return (RouteEndpoint)builder.Build();
    }
}

