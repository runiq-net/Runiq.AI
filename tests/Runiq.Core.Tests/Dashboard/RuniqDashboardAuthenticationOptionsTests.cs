using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Runiq.Core.Dashboard;

namespace Runiq.Core.Tests.Dashboard;

/// <summary>
/// Dashboard authentication options ve validation davranışlarını doğrulayan testleri içerir.
/// </summary>
public sealed class RuniqDashboardAuthenticationOptionsTests
{
    [Fact]
    public void ValidateAuthentication_ShouldThrow_WhenAuthenticationIsNotConfigured()
    {
        // Dashboard kullanıldığında auth kararı verilmemişse net validation hatası üretildiğini doğrular.
        var options = new RuniqDashboardOptions();

        var exception = Assert.Throws<InvalidOperationException>(
            options.ValidateAuthentication);

        Assert.Contains("auth.RequireRole", exception.Message);
        Assert.Contains("auth.RequireAuthenticatedUser", exception.Message);
        Assert.Contains("auth.AllowAnonymous", exception.Message);
    }

    [Fact]
    public void AllowAnonymous_ShouldBeValid()
    {
        // Anonim erişim bilinçli seçildiğinde validation'ın geçtiğini doğrular.
        var options = new RuniqDashboardOptions();

        options.Authentication(auth => auth.AllowAnonymous());
        options.ValidateAuthentication();

        Assert.Equal(
            RuniqDashboardAccessMode.Anonymous,
            options.AuthenticationOptions.AccessMode);
    }

    [Fact]
    public void RequireAuthenticatedUser_ShouldBeValid()
    {
        // Authenticated kullanıcı gereksinimi seçildiğinde validation'ın geçtiğini doğrular.
        var options = new RuniqDashboardOptions();

        options.Authentication(auth => auth.RequireAuthenticatedUser());
        options.ValidateAuthentication();

        Assert.Equal(
            RuniqDashboardAccessMode.AuthenticatedUser,
            options.AuthenticationOptions.AccessMode);
    }

    [Fact]
    public void RequireRole_ShouldBeValid_WhenSingleRoleIsConfigured()
    {
        // Tek role ile role tabanlı erişimin geçerli olduğunu doğrular.
        var options = new RuniqDashboardOptions();

        options.Authentication(auth => auth.RequireRole("Admin"));
        options.ValidateAuthentication();

        Assert.Equal(
            RuniqDashboardAccessMode.Role,
            options.AuthenticationOptions.AccessMode);
        Assert.Equal(["Admin"], options.AuthenticationOptions.Roles);
    }

    [Fact]
    public void RequireRole_ShouldBeValid_WhenMultipleRolesAreConfigured()
    {
        // Birden fazla role ile role tabanlı erişimin geçerli olduğunu doğrular.
        var options = new RuniqDashboardOptions();

        options.Authentication(auth => auth.RequireRole("Admin", "Developer", "Ops"));
        options.ValidateAuthentication();

        Assert.Equal(["Admin", "Developer", "Ops"], options.AuthenticationOptions.Roles);
    }

    [Fact]
    public void RequireRole_ShouldThrow_WhenNoRolesAreProvided()
    {
        // Role listesi boş bırakılırsa hata üretildiğini doğrular.
        var auth = new RuniqDashboardAuthenticationOptions();

        var exception = Assert.Throws<ArgumentException>(() => auth.RequireRole());

        Assert.Contains("at least one role", exception.Message);
    }

    [Fact]
    public void RequireRole_ShouldThrow_WhenRolesArgumentIsNull()
    {
        // Role parametre dizisinin null verilmesinin geçersiz olduğunu doğrular.
        var auth = new RuniqDashboardAuthenticationOptions();

        Assert.Throws<ArgumentNullException>(() => auth.RequireRole(null!));
    }

    [Fact]
    public void RequireRole_ShouldThrow_WhenRoleListContainsNull()
    {
        // Role listesi içinde null değer bulunmasının geçersiz olduğunu doğrular.
        var auth = new RuniqDashboardAuthenticationOptions();

        var exception = Assert.Throws<ArgumentException>(() =>
            auth.RequireRole("Admin", null!));

        Assert.Contains("cannot contain null", exception.Message);
    }

    [Fact]
    public void RequireRole_ShouldThrow_WhenOnlyWhitespaceRolesAreProvided()
    {
        // Boş veya whitespace role değerlerinin geçersiz olduğunu doğrular.
        var auth = new RuniqDashboardAuthenticationOptions();

        var exception = Assert.Throws<ArgumentException>(() =>
            auth.RequireRole(" ", ""));

        Assert.Contains("cannot contain null", exception.Message);
    }

    [Fact]
    public void RequireRole_ShouldTrimAndDeduplicateRolesCaseInsensitively()
    {
        // Role değerlerinin trim edilip case-insensitive distinct edildiğini doğrular.
        var auth = new RuniqDashboardAuthenticationOptions();

        auth.RequireRole("Admin", "admin", " Developer ");

        Assert.Equal(["Admin", "Developer"], auth.Roles);
    }

    [Fact]
    public void RequireRole_ShouldThrow_WhenAllowAnonymousWasAlreadyConfigured()
    {
        // Anonim erişimden sonra role gereksinimi seçilemeyeceğini doğrular.
        var auth = new RuniqDashboardAuthenticationOptions();

        auth.AllowAnonymous();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            auth.RequireRole("Admin"));

        Assert.Contains("already been configured", exception.Message);
    }

    [Fact]
    public void AllowAnonymous_ShouldThrow_WhenRequireRoleWasAlreadyConfigured()
    {
        // Role gereksiniminden sonra anonim erişim seçilemeyeceğini doğrular.
        var auth = new RuniqDashboardAuthenticationOptions();

        auth.RequireRole("Admin");

        var exception = Assert.Throws<InvalidOperationException>(
            auth.AllowAnonymous);

        Assert.Contains("already been configured", exception.Message);
    }

    [Fact]
    public void RequireRole_ShouldThrow_WhenRequireAuthenticatedUserWasAlreadyConfigured()
    {
        // Authenticated kullanıcı gereksiniminden sonra role gereksinimi seçilemeyeceğini doğrular.
        var auth = new RuniqDashboardAuthenticationOptions();

        auth.RequireAuthenticatedUser();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            auth.RequireRole("Admin"));

        Assert.Contains("already been configured", exception.Message);
    }

    [Fact]
    public void UseRuniqDashboard_ShouldThrow_WhenAuthenticationIsNotConfigured()
    {
        // Dashboard middleware aktifken auth kararı verilmezse startup'ta hata üretildiğini doğrular.
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using var _ = new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
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
                            });
                        });
                })
                .Start();
        });

        Assert.Contains("Runiq Dashboard authentication", exception.Message);
    }
}
