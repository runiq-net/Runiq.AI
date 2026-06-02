# Runiq Dashboard Security: Authenticated User

This sample shows how to protect the embedded Runiq Dashboard with the host application's ASP.NET Core Cookie Authentication setup.

Runiq does not provide a separate authentication system for the dashboard. The dashboard uses the authenticated `HttpContext.User` created by the host ASP.NET Core application.

## Security Model

The dashboard is configured to allow any signed-in user:

```csharp
app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Dashboard - Authenticated User";

    options.Authentication(auth =>
    {
        auth.RequireAuthenticatedUser();
    });
});
```

Anonymous requests to `/dashboard` are challenged by Cookie Authentication and redirected to `/login`.

## Authentication Setup

The sample uses:

- ASP.NET Core Cookie Authentication
- A small in-memory test user authenticator
- A simple MVC login/logout flow
- No database
- No EF Core
- No ASP.NET Core Identity

The important host setup is:

```csharp
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
    });

builder.Services.AddAuthorization();
```

The middleware order matters:

```csharp
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.UseRuniqDashboard(...);
```

## Test User

```text
user@runiq.dev / Password123!
```

## Run

```powershell
dotnet run --project samples/Runiq.DashboardSecurityUser/Runiq.DashboardSecurityUser.csproj
```

## Manual Test

1. Open `/dashboard`.
2. Confirm that the browser is redirected to `/login`.
3. Sign in with `user@runiq.dev / Password123!`.
4. Confirm that the dashboard is accessible.
5. Click logout.
6. Open `/dashboard` again and confirm that login is required.

## Production Notes

This sample keeps the user store in memory only to keep the dashboard security flow easy to read. A production application should use its normal authentication provider, user store, cookie settings, HTTPS policy, and operational security controls.
