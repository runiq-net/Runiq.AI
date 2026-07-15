using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Runiq.AI.DashboardSecurityUser.Models;
using Runiq.AI.DashboardSecurityUser.Services;

namespace Runiq.AI.DashboardSecurityUser.Controllers;

/// <summary>
/// Cookie Authentication login/logout akışını yöneten sample controller.
/// </summary>
public sealed class AccountController : Controller
{
    private readonly TestUserAuthenticator authenticator;

    public AccountController(TestUserAuthenticator authenticator)
    {
        this.authenticator = authenticator;
    }

    [HttpGet("/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        // Login sayfası host uygulamanın authentication sorumluluğunu gösterir.
        return View(new LoginViewModel
        {
            ReturnUrl = ReturnUrlHelper.GetSafeReturnUrl(returnUrl)
        });
    }

    [HttpPost("/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // Test user:
        // user@runiq.dev / Password123!
        model.ReturnUrl = ReturnUrlHelper.GetSafeReturnUrl(model.ReturnUrl);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var principal = authenticator.Authenticate(model.Email, model.Password);

        if (principal is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal);

        return LocalRedirect(model.ReturnUrl);
    }

    [HttpGet("/logout")]
    public async Task<IActionResult> Logout()
    {
        // Cookie temizlendikten sonra dashboard tekrar login ister.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return LocalRedirect("/login?ReturnUrl=/dashboard");
    }
}
