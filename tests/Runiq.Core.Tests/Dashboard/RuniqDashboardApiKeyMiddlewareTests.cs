using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runiq.Core;
using System.Net;
using System.Net.Http.Headers;

namespace Runiq.Core.Tests.Dashboard;

/// <summary>
/// Dashboard API key middleware davranışlarını doğrulayan testleri içerir.
/// </summary>
[Collection("Dashboard assets")]
public sealed class RuniqDashboardApiKeyMiddlewareTests
{
    private const string TestApiKey = "test-secret-key-12345";

    // ─── Auth yokken (backward compatibility) ───

    [Fact]
    public async Task ApiEndpoint_WithoutApiKeyConfigured_ShouldAllowAccess()
    {
        // ApiKey ayarlanmadığında, API endpoint'lerine kimlik doğrulaması olmadan erişilebilmelidir.
        using var server = CreateServer(apiKey: null);

        var response = await server.GetTestClient()
            .GetAsync("/dashboard/metadata/agents");

        response.EnsureSuccessStatusCode();
    }

    // ─── Auth varken — yetkisiz istekler ───

    [Fact]
    public async Task ApiEndpoint_WithApiKeyConfigured_ShouldReturn401_WhenNoKeyProvided()
    {
        // ApiKey ayarlandığında, header olmadan yapılan istekler 401 dönmelidir.
        using var server = CreateServer(apiKey: TestApiKey);

        var response = await server.GetTestClient()
            .GetAsync("/dashboard/metadata/agents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoint_WithApiKeyConfigured_ShouldReturn401_WhenWrongKeyProvided()
    {
        // Yanlış API key ile yapılan istekler 401 dönmelidir.
        using var server = CreateServer(apiKey: TestApiKey);

        var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.GetAsync("/dashboard/metadata/agents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ToolRunEndpoint_WithApiKeyConfigured_ShouldReturn401_WhenNoKeyProvided()
    {
        // Tool run endpoint'i de auth gerektirmelidir.
        using var server = CreateServer(apiKey: TestApiKey);

        var content = new StringContent(
            """{"input": {"value": "test"}}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await server.GetTestClient()
            .PostAsync("/dashboard/api/tools/test_tool/run", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── Auth varken — yetkili istekler ───

    [Fact]
    public async Task ApiEndpoint_WithApiKeyConfigured_ShouldReturn200_WhenCorrectBearerTokenProvided()
    {
        // Doğru Bearer token ile istekler başarılı olmalıdır.
        using var server = CreateServer(apiKey: TestApiKey);

        var client = server.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestApiKey);

        var response = await client.GetAsync("/dashboard/metadata/agents");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ApiEndpoint_WithApiKeyConfigured_ShouldReturn200_WhenCorrectXApiKeyProvided()
    {
        // Doğru X-Api-Key header ile istekler başarılı olmalıdır.
        using var server = CreateServer(apiKey: TestApiKey);

        var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey);

        var response = await client.GetAsync("/dashboard/metadata/agents");

        response.EnsureSuccessStatusCode();
    }

    // ─── SPA sayfaları auth gerektirmemeli ───

    [Fact]
    public async Task DashboardPage_WithApiKeyConfigured_ShouldNotRequireAuth()
    {
        // Dashboard SPA sayfası auth olmadan erişilebilir olmalıdır (401 dönmemeli).
        using var server = CreateServer(apiKey: TestApiKey);

        var response = await server.GetTestClient()
            .GetAsync("/dashboard/some-page");

        // SPA sayfaları auth gerektirmemeli — 401 dönmediğini doğrula.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        response.EnsureSuccessStatusCode();
    }

    // ─── 401 response formatı ───

    [Fact]
    public async Task ApiEndpoint_WithApiKeyConfigured_ShouldReturnJsonError_WhenUnauthorized()
    {
        // 401 response'u JSON formatında hata mesajı içermelidir.
        using var server = CreateServer(apiKey: TestApiKey);

        var response = await server.GetTestClient()
            .GetAsync("/dashboard/metadata/agents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unauthorized", body);
        Assert.Contains("API key", body);
    }

    // ─── Server factory ───

    private static IHost CreateServer(string? apiKey)
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
                        services.AddRuniqServer();
                    })
                    .Configure(app =>
                    {
                        app.UseRuniqDashboard(options =>
                        {
                            options.Path = "/dashboard";
                            options.Title = "Test Dashboard";
                            options.ApiKey = apiKey;
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
}
