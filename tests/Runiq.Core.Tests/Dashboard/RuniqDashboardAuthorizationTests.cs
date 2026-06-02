using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Runiq.Core.Tests.Dashboard;

/// <summary>
/// Dashboard UI ve API erişim kurallarını doğrulayan testleri içerir.
/// </summary>
[Collection("Dashboard assets")]
public sealed class RuniqDashboardAuthorizationTests
{
    [Theory]
    [InlineData("/dashboard/agents")]
    [InlineData("/dashboard/metadata/agents")]
    public async Task Dashboard_ShouldReturnUnauthorized_WhenAuthenticatedUserIsRequiredAndUserIsAnonymous(
        string path)
    {
        // AuthenticatedUser kuralında anonim kullanıcının UI ve API erişiminin reddedildiğini doğrular.
        using var server = CreateServer(auth => auth.RequireAuthenticatedUser());

        var response = await server.GetTestClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/dashboard/agents")]
    [InlineData("/dashboard/metadata/agents")]
    public async Task Dashboard_ShouldAllowAccess_WhenAuthenticatedUserIsRequiredAndUserIsAuthenticated(
        string path)
    {
        // AuthenticatedUser kuralında authenticated kullanıcının UI ve API erişiminin kabul edildiğini doğrular.
        using var server = CreateServer(auth => auth.RequireAuthenticatedUser());
        var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "developer");

        var response = await client.GetAsync(path);

        response.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("/dashboard/agents")]
    [InlineData("/dashboard/metadata/agents")]
    public async Task Dashboard_ShouldReturnForbidden_WhenRequiredRoleIsMissing(
        string path)
    {
        // Role kuralında authenticated ama yetkisiz kullanıcının UI ve API erişiminin reddedildiğini doğrular.
        using var server = CreateServer(auth => auth.RequireRole("Admin", "Ops"));
        var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "developer");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Developer");

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/dashboard/agents")]
    [InlineData("/dashboard/metadata/agents")]
    public async Task Dashboard_ShouldAllowAccess_WhenAnyRequiredRoleMatches(
        string path)
    {
        // Role kuralında rollerden herhangi biri eşleşirse UI ve API erişiminin kabul edildiğini doğrular.
        using var server = CreateServer(auth => auth.RequireRole("Admin", "Ops"));
        var client = server.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "ops-user");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Developer,Ops");

        var response = await client.GetAsync(path);

        response.EnsureSuccessStatusCode();
    }

    private static IHost CreateServer(
        Action<Runiq.Core.Dashboard.RuniqDashboardAuthenticationOptions> configureAuthentication)
    {
        var dashboardRoot = PrepareDashboardAssets();

        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseContentRoot(dashboardRoot)
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddRuniqServer();
                    })
                    .Configure(app =>
                    {
                        app.Use(async (context, next) =>
                        {
                            var userName = context.Request.Headers["X-Test-User"].ToString();

                            if (!string.IsNullOrWhiteSpace(userName))
                            {
                                var claims = new List<Claim>
                                {
                                    new(ClaimTypes.Name, userName)
                                };

                                var roles = context.Request.Headers["X-Test-Roles"]
                                    .ToString()
                                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                                claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

                                context.User = new ClaimsPrincipal(
                                    new ClaimsIdentity(claims, authenticationType: "Test"));
                            }

                            await next();
                        });

                        app.UseRuniqDashboard(options =>
                        {
                            options.Path = "/dashboard";
                            options.Title = "Test Dashboard";
                            options.Authentication(configureAuthentication);
                        });
                    });
            })
            .Start();
    }

    private static string PrepareDashboardAssets()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "Studio",
            "wwwroot");

        Directory.CreateDirectory(root);

        File.WriteAllText(
            Path.Combine(root, "index.html"),
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

        return AppContext.BaseDirectory;
    }
}
