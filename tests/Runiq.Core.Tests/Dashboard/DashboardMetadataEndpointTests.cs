using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Runiq.Agents;
using Runiq.ContextSpaces.Models.Sources;
using Runiq.Core;
using Runiq.Core.Configuration;

namespace Runiq.Core.Tests.Dashboard;

/// <summary>
/// Dashboard metadata endpoint davranışlarını doğrulayan testleri içerir.
/// </summary>
[Collection("Dashboard assets")]
public sealed class DashboardMetadataEndpointTests
{
    [Fact]
    public async Task MetadataAgentsEndpoint_ShouldReturnRegisteredAgents()
    {
        // Dashboard metadata endpoint'inin kayıtlı agent bilgisini JSON olarak döndürdüğünü doğrular.
        using var server = CreateServer();

        var response = await server.CreateClient().GetAsync("/dashboard/metadata/agents");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var agents = document.RootElement;

        Assert.Equal(JsonValueKind.Array, agents.ValueKind);
        Assert.Single(agents.EnumerateArray());

        var agent = agents[0];

        Assert.Equal("test-agent", agent.GetProperty("id").GetString());
        Assert.Equal("Test Agent", agent.GetProperty("name").GetString());
        Assert.Equal("Test instructions.", agent.GetProperty("instructions").GetString());
        Assert.Equal("openai/gpt-5", agent.GetProperty("model").GetString());
        Assert.Equal("minimal", agent.GetProperty("reasoningEffort").GetString());
        Assert.Equal("low", agent.GetProperty("verbosity").GetString());

        var contextSpaces = agent.GetProperty("contextSpaces");

        Assert.Equal(JsonValueKind.Array, contextSpaces.ValueKind);
        Assert.Single(contextSpaces.EnumerateArray());

        var contextSpace = contextSpaces[0];

        Assert.Equal("test-context", contextSpace.GetProperty("id").GetString());
        Assert.Equal("Test Context", contextSpace.GetProperty("name").GetString());
        Assert.Equal("Test context description.", contextSpace.GetProperty("description").GetString());
    }

    [Fact]
    public async Task MetadataContextSpacesEndpoint_ShouldReturnRegisteredContextSpaces()
    {
        // Dashboard metadata endpoint'inin kayıtlı context space bilgisini JSON olarak döndürdüğünü doğrular.
        using var server = CreateServer();

        var response = await server.CreateClient().GetAsync("/dashboard/metadata/context-spaces");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var contextSpaces = document.RootElement;

        Assert.Equal(JsonValueKind.Array, contextSpaces.ValueKind);
        Assert.Single(contextSpaces.EnumerateArray());

        var contextSpace = contextSpaces[0];

        Assert.Equal("test-context", contextSpace.GetProperty("id").GetString());
        Assert.Equal("Test Context", contextSpace.GetProperty("name").GetString());
        Assert.Equal("Test context description.", contextSpace.GetProperty("description").GetString());

        var sources = contextSpace.GetProperty("sources");

        Assert.Equal(JsonValueKind.Array, sources.ValueKind);
        Assert.Single(sources.EnumerateArray());

        var source = sources[0];

        Assert.Equal("test-source", source.GetProperty("id").GetString());
        Assert.Equal("Test Source", source.GetProperty("name").GetString());
        Assert.Equal("UploadedDocuments", source.GetProperty("kind").GetString());
        Assert.Equal("Test source description.", source.GetProperty("description").GetString());

        var attachedAgents = contextSpace.GetProperty("attachedAgents");

        Assert.Equal(JsonValueKind.Array, attachedAgents.ValueKind);
        Assert.Single(attachedAgents.EnumerateArray());

        var attachedAgent = attachedAgents[0];

        Assert.Equal("test-agent", attachedAgent.GetProperty("id").GetString());
        Assert.Equal("Test Agent", attachedAgent.GetProperty("name").GetString());
    }

    [Fact]
    public async Task MetadataAgentsEndpoint_ShouldBeCaseInsensitive()
    {
        // Metadata endpoint path karşılaştırmasının case-insensitive çalıştığını doğrular.
        using var server = CreateServer();

        var response = await server.CreateClient().GetAsync("/dashboard/METADATA/AGENTS");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Single(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task UnknownDashboardApiEndpoint_ShouldReturnNotFound()
    {
        // Dashboard API altında tanımlı olmayan endpoint'lerin SPA fallback'e düşmeden 404 döndüğünü doğrular.
        using var server = CreateServer();

        var response = await server.CreateClient().GetAsync("/dashboard/api/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("Runiq endpoint was not found.", body);
    }

    [Fact]
    public async Task UnknownDashboardMetadataEndpoint_ShouldReturnNotFound()
    {
        // Dashboard metadata altında tanımlı olmayan endpoint'lerin SPA fallback'e düşmeden 404 döndüğünü doğrular.
        using var server = CreateServer();

        var response = await server.CreateClient().GetAsync("/dashboard/metadata/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("Runiq endpoint was not found.", body);
    }

    private static TestServer CreateServer()
    {
        PrepareDashboardAssets();

        var builder = new WebHostBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices(services =>
            {
                services.AddRouting();

                services.AddRuniqServer(options =>
                {
                    options.AddContextSpace(new ContextSpace(
                            id: "test-context",
                            name: "Test Context",
                            description: "Test context description.")
                        .AddSource(new ContextSpaceSource(
                            id: "test-source",
                            name: "Test Source",
                            kind: ContextSpaceSourceKind.UploadedDocuments,
                            description: "Test source description.")));

                    options.AddAgent(new Agent(
                            id: "test-agent",
                            name: "Test Agent",
                            instructions: "Test instructions.",
                            model: "openai/gpt-5",
                            apiKey: "test-key")
                        .UseContextSpace("test-context"));
                });
            })
            .Configure(app =>
            {
                app.UseRuniqDashboard(options =>
                {
                    options.Path = "/dashboard";
                    options.Title = "Test Dashboard";
                });
            });

        return new TestServer(builder);
    }

    private static void PrepareDashboardAssets()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "Studio",
            "wwwroot");

        Directory.CreateDirectory(root);

        var indexPath = Path.Combine(root, "index.html");

        File.WriteAllText(
            indexPath,
            """
            <!doctype html>
            <html>
            <head>
                <title>__RUNIQ_TITLE_HTML__</title>
                <script>
                    window.__RUNIQ_DASHBOARD__ = __RUNIQ_DASHBOARD_CONFIG__;
                </script>
            </head>
            <body>Runiq Dashboard</body>
            </html>
            """);
    }
}