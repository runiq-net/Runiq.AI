# Runiq Dashboard Security: Role-Based Access

This sample shows how to protect the embedded Runiq Dashboard with the host application's ASP.NET Core Cookie Authentication and role claims.

Runiq does not provide a separate role system for the dashboard. The dashboard evaluates the roles available on the host application's authenticated `HttpContext.User`.

## Security Model

The dashboard is configured to allow only users with the `Admin` role:

```csharp
app.UseRuniqDashboard(options =>
{
    options.Path = "/dashboard";
    options.Title = "Runiq Dashboard - Admin Only";

    options.Authentication(auth =>
    {
        auth.RequireRole("Admin");
    });
});
```

Anonymous requests to `/dashboard` are challenged by Cookie Authentication and redirected to `/login`.

Signed-in users without the `Admin` role are forbidden and redirected to `/access-denied` by the host Cookie Authentication configuration.

## Authentication Setup

The sample uses:

- ASP.NET Core Cookie Authentication
- Role claims on the signed-in user
- A small in-memory test user authenticator
- A simple MVC login/logout/access denied flow
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
        options.AccessDeniedPath = "/access-denied";
    });

builder.Services.AddAuthorization();
```

The authenticated Admin user receives a role claim:

```csharp
new Claim(ClaimTypes.Role, "Admin")
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

## Test Users

```text
admin@runiq.dev / Password123! -> Admin role
user@runiq.dev  / Password123! -> No Admin role
```

## Run

```powershell
dotnet run --project samples/Runiq.DashboardSecurityRole/Runiq.DashboardSecurityRole.csproj
```

## Manual Test

1. Open `/dashboard`.
2. Confirm that the browser is redirected to `/login`.
3. Sign in with `user@runiq.dev / Password123!`.
4. Confirm that `/access-denied` is shown.
5. Logout.
6. Sign in with `admin@runiq.dev / Password123!`.
7. Confirm that the dashboard is accessible.
8. Logout and confirm that `/dashboard` requires login again.

## Production Notes

This sample keeps users and roles in memory only to keep the dashboard security flow easy to read. A production application should use its normal authentication provider, role source, cookie settings, HTTPS policy, and operational security controls.
