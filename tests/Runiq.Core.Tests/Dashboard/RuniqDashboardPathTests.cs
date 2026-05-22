using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Runiq.Core;
using Runiq.Core.Configuration;

namespace Runiq.Core.Tests.Dashboard;

public sealed class RuniqDashboardPathTests
{
    [Fact]
    public async Task UseRuniqDashboard_ShouldInjectConfiguredBasePath_WhenPathAlreadyStartsWithSlash()
    {
        // Dashboard path değeri slash ile verildiğinde base path'in index.html içine doğru enjekte edildiğini doğrular.
        using var server = CreateServer("/dashboard");

        var response = await server.CreateClient().GetAsync("/dashboard/agents");

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("basePath: '/dashboard'", html);
        Assert.Contains("title: 'Test Dashboard'", html);
    }

    [Fact]
    public async Task UseRuniqDashboard_ShouldNormalizeConfiguredBasePath_WhenPathDoesNotStartWithSlash()
    {
        // Dashboard path değeri slash olmadan verildiğinde başına slash eklenerek normalize edildiğini doğrular.
        using var server = CreateServer("dashboard");

        var response = await server.CreateClient().GetAsync("/dashboard/agents");

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("basePath: '/dashboard'", html);
    }

    [Fact]
    public async Task UseRuniqDashboard_ShouldTrimTrailingSlash_WhenPathEndsWithSlash()
    {
        // Dashboard path değeri trailing slash ile verildiğinde canonical base path'in slash olmadan kullanıldığını doğrular.
        using var server = CreateServer("/dashboard/");

        var response = await server.CreateClient().GetAsync("/dashboard/agents");

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("basePath: '/dashboard'", html);
        Assert.DoesNotContain("basePath: '/dashboard/'", html);
    }

    [Fact]
    public async Task UseRuniqDashboard_ShouldRedirectToTrailingSlash_WhenRequestMatchesBasePathExactly()
    {
        // Dashboard base path'e slash olmadan gelindiğinde canonical slash'lı adrese yönlendirme yapıldığını doğrular.
        using var server = CreateServer("/dashboard");

        var client = server.CreateClient();
        client.DefaultRequestHeaders.Clear();

        var response = await client.GetAsync("/dashboard");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard/", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UseRuniqDashboard_ShouldThrowArgumentException_WhenPathIsInvalid(string path)
    {
        // Boş veya root dashboard path değerlerinin desteklenmediğini doğrular.
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            using var _ = CreateServer(path);
        });

        Assert.Contains("Dashboard", exception.Message);
    }

    private static TestServer CreateServer(string dashboardPath)
    {
        var dashboardRoot = PrepareDashboardAssets();

        var builder = new WebHostBuilder()
            .UseContentRoot(dashboardRoot)
              .ConfigureServices(services =>
              {
                  services.AddRouting();
                  services.AddRuniqServer();
              })
            .Configure(app =>
            {
                app.UseRuniqDashboard(options =>
                {
                    options.Path = dashboardPath;
                    options.Title = "Test Dashboard";
                });
            });

        return new TestServer(builder);
    }

    private static string PrepareDashboardAssets()
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

        return AppContext.BaseDirectory;
    }
}