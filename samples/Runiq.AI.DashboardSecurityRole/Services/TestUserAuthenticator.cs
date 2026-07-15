using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Runiq.AI.DashboardSecurityRole.Services;

/// <summary>
/// Veritabani/Identity kullanmadan sample test kullanicilarini dogrular.
/// </summary>
public sealed class TestUserAuthenticator
{
    public ClaimsPrincipal? Authenticate(string email, string password)
    {
        if (password != "Password123!")
        {
            return null;
        }

        var role = GetRole(email);

        if (role is null && !IsRegularUser(email))
        {
            return null;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, email),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Email, email)
        };

        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static string? GetRole(string email)
    {
        return string.Equals(email, "admin@runiq.dev", StringComparison.OrdinalIgnoreCase)
            ? "Admin"
            : null;
    }

    private static bool IsRegularUser(string email)
    {
        return string.Equals(email, "user@runiq.dev", StringComparison.OrdinalIgnoreCase);
    }
}
