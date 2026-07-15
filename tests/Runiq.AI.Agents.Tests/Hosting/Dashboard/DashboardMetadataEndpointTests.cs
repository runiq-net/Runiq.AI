using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core;
using Runiq.AI.Core.Configuration;
using System.Net;
using System.Text.Json;

namespace Runiq.AI.Core.Tests.Dashboard;

/// <summary>
/// Dashboard metadata endpoint davranislarini dogrulayan testleri içerir.
/// </summary>
[Collection("Dashboard assets")]
public sealed class DashboardMetadataEndpointTests
{
    [Fact]
    public async Task MetadataAgentsEndpoint_ShouldReturnRegisteredAgents()
    {
        // Dashboard metadata endpoint'inin kayitli agent bilgisini JSON olarak döndürdügünü dogrular.
        using var server = CreateServer();

        var response = await server.GetTestClient().GetAsync("/dashboard/metadata/agents");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var agents = document.RootElement;

        Assert.Equal(JsonValueKind.Array, agents.ValueKind);
        Assert.Equal(2, agents.GetArrayLength());

        var agent = agents
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "test-agent");

        Assert.Equal("test-agent", agent.GetProperty("id").GetString());
        Assert.Equal("Test Agent", agent.GetProperty("name").GetString());
        Assert.Equal("Test instructions.", agent.GetProperty("instructions").GetString());
        Assert.Equal("openai/gpt-5", agent.GetProperty("model").GetString());
        Assert.Equal("minimal", agent.GetProperty("reasoningEffort").GetString());
        Assert.Equal("low", agent.GetProperty("verbosity").GetString());
        var rag = agent.GetProperty("rag");
        Assert.True(rag.GetProperty("enabled").GetBoolean());
        Assert.Equal("documents", rag.GetProperty("indexName").GetString());
        Assert.Equal("Required", rag.GetProperty("executionMode").GetString());


        var tools = agent.GetProperty("tools");

        Assert.Equal(JsonValueKind.Array, tools.ValueKind);
        Assert.Single(tools.EnumerateArray());

        var tool = tools[0];

        Assert.Equal("test_tool", tool.GetProperty("name").GetString());
        Assert.Equal("Test Tool", tool.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task MetadataAgentsEndpoint_ShouldBeCaseInsensitive()
    {
        // Metadata endpoint path karsilastirmasinin case-insensitive çalistigini dogrular.
        using var server = CreateServer();

        var response = await server.GetTestClient().GetAsync("/dashboard/METADATA/AGENTS");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(2, document.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task UnknownDashboardApiEndpoint_ShouldReturnNotFound()
    {
        // Dashboard API altinda tanimli olmayan endpoint'lerin SPA fallback'e düsmeden 404 döndügünü dogrular.
        using var server = CreateServer();

        var response = await server.GetTestClient().GetAsync("/dashboard/api/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("Runiq endpoint was not found.", body);
    }

    [Fact]
    public async Task UnknownDashboardMetadataEndpoint_ShouldReturnNotFound()
    {
        // Dashboard metadata altinda tanimli olmayan endpoint'lerin SPA fallback'e düsmeden 404 döndügünü dogrular.
        using var server = CreateServer();

        var response = await server.GetTestClient().GetAsync("/dashboard/metadata/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("Runiq endpoint was not found.", body);
    }

    [Fact]
    public async Task MetadataTeamsEndpoint_ShouldReturnNotFound()
    {
        // Agent Team metadata endpoint'inin aktif dashboard metadata yüzeyinden kaldirildigini dogrular.
        using var server = CreateServer();

        var response = await server.GetTestClient().GetAsync("/dashboard/metadata/teams");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static IHost CreateServer()
    {
        PrepareDashboardAssets();

        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseContentRoot(AppContext.BaseDirectory)
                    .UseTestServer()
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
                                    apiKey: "test-key")
                                .AddTool<TestTool>()
                                .UseRag(rag =>
                                {
                                    rag.IndexName = "documents";
                                    rag.Mode = RagExecutionMode.Required;
                                }));

                            options.AddAgent(new Agent(
                                id: "planner-agent",
                                name: "Planner Agent",
                                instructions: "Create practical plans from provided research.",
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
                            options.Authentication(auth => auth.AllowAnonymous());
                        });
                    });
            })
            .Start();
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

    [RuniqTool("test_tool", "Test tool.")]
    private sealed class TestTool : IRuniqTool<TestToolInput, TestToolOutput>
    {
        public Task<TestToolOutput> ExecuteAsync(
            TestToolInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestToolOutput(input.Value));
        }
    }

    private sealed record TestToolInput(string Value);

    private sealed record TestToolOutput(string Value);
}

