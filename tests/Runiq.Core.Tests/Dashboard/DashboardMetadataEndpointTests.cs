using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Runiq.Agents;
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
        // Dashboard API altında tanımlı olmayan endpoint'lerin 404 döndüğünü doğrular.
        using var server = CreateServer();

        var response = await server.CreateClient().GetAsync("/dashboard/api/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("Runiq API endpoint was not found.", body);
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
                    options.AddAgent(new Agent(
                        id: "test-agent",
                        name: "Test Agent",
                        instructions: "Test instructions.",
                        model: "openai/gpt-5",
                        apiKey: "test-key"));
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
                <title>__RUNIQ_TITLE__</title>
                <script>
                    window.__RUNIQ_DASHBOARD__ = {
                        basePath: '__RUNIQ_BASE_PATH__',
                        title: '__RUNIQ_TITLE__'
                    };
                </script>
            </head>
            <body>Runiq Dashboard</body>
            </html>
            """);
    }
}