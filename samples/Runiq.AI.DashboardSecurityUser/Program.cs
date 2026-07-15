using Microsoft.AspNetCore.Authentication.Cookies;
using Runiq.AI.Core;
using Runiq.AI.DashboardSecurityUser.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<TestUserAuthenticator>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Anonymous dashboard istekleri host uygulamanın login sayfasına yönlenir.
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
    });

builder.Services.AddAuthorization();
builder.Services.AddRuniqServer(_ => { });

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
    options.Title = "Runiq Dashboard - Authenticated User";

    options.Authentication(auth =>
    {
        auth.RequireAuthenticatedUser();
    });
});

app.Run();

