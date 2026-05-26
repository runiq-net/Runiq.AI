using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Runiq.Core;

namespace Runiq.Core.Tests.Dashboard;

/// <summary>
/// Dashboard agent team chat endpoint davranışlarını doğrulayan testleri içerir.
/// </summary>
[Collection("Dashboard assets")]
public sealed class DashboardTeamChatEndpointTests
{
    /// <summary>
    /// Bilinmeyen agent team için stream endpoint'inin team_failed event'i döndürdüğünü doğrular.
    /// </summary>
    [Fact]
    public async Task TeamChatEndpoint_ShouldReturnTeamFailedEvent_WhenTeamDoesNotExist()
    {
        using var server = CreateServer();

        using var request = new StringContent(
            """
            {
              "message": "Create a travel plan."
            }
            """,
            Encoding.UTF8,
            "application/json");

        var response = await server
            .CreateClient()
            .PostAsync("/dashboard/api/teams/missing-team/chat", request);

        response.EnsureSuccessStatusCode();

        Assert.StartsWith(
            "text/event-stream",
            response.Content.Headers.ContentType?.ToString(),
            StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("data: ", body, StringComparison.Ordinal);
        Assert.Contains("data: [DONE]", body, StringComparison.Ordinal);

        var payload = ReadFirstSseJsonPayload(body);

        Assert.Equal("team_failed", payload.GetProperty("type").GetString());
        Assert.Equal("missing-team", payload.GetProperty("teamId").GetString());
        Assert.Equal("TeamNotFound", payload.GetProperty("errorCode").GetString());
        Assert.Equal(
            "Agent team 'missing-team' was not found.",
            payload.GetProperty("errorMessage").GetString());
    }

    /// <summary>
    /// Boş mesaj gönderildiğinde endpoint'in bad request döndürdüğünü doğrular.
    /// </summary>
    [Fact]
    public async Task TeamChatEndpoint_ShouldReturnBadRequest_WhenMessageIsEmpty()
    {
        using var server = CreateServer();

        using var request = new StringContent(
            """
            {
              "message": " "
            }
            """,
            Encoding.UTF8,
            "application/json");

        var response = await server
            .CreateClient()
            .PostAsync("/dashboard/api/teams/travel-team/chat", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;

        Assert.False(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("MessageRequired", root.GetProperty("errorCode").GetString());
        Assert.Equal("Message is required.", root.GetProperty("errorMessage").GetString());
    }

    private static TestServer CreateServer()
    {
        PrepareDashboardAssets();

        var builder = new WebHostBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices(services =>
            {
                services.AddRouting();

                services.AddRuniqServer(_ =>
                {
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

    private static JsonElement ReadFirstSseJsonPayload(string body)
    {
        var firstDataLine = body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .First(line =>
                line.StartsWith("data: ", StringComparison.Ordinal) &&
                !line.Equals("data: [DONE]", StringComparison.Ordinal));

        var json = firstDataLine["data: ".Length..];

        using var document = JsonDocument.Parse(json);

        return document.RootElement.Clone();
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