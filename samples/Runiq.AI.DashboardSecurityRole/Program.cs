using Microsoft.AspNetCore.Authentication.Cookies;
using Runiq.AI.Core;
using Runiq.AI.DashboardSecurityRole.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<TestUserAuthenticator>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Dashboard auth kararini host uygulamanin cookie auth akisi yonetir.
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
    });

builder.Services.AddAuthorization();
builder.Services.AddRuniqServer();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Dashboard - Admin Only";

    options.Authentication(auth =>
    {
        auth.RequireRole("Admin");
    });
});

app.Run();

