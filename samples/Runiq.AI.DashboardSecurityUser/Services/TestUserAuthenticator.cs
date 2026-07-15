using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Runiq.AI.DashboardSecurityUser.Services;

/// <summary>
/// Veritabanı/Identity kullanmadan sample test kullanıcısını doğrular.
/// </summary>
public sealed class TestUserAuthenticator
{
    public ClaimsPrincipal? Authenticate(string email, string password)
    {
        if (!IsValidUser(email, password))
        {
            return null;
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, email),
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Email, email)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static bool IsValidUser(string email, string password)
    {
        return string.Equals(email, "user@runiq.dev", StringComparison.OrdinalIgnoreCase) &&
            password == "Password123!";
    }
}
