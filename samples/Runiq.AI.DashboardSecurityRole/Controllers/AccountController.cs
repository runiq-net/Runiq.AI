using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Runiq.AI.DashboardSecurityRole.Models;
using Runiq.AI.DashboardSecurityRole.Services;

namespace Runiq.AI.DashboardSecurityRole.Controllers;

/// <summary>
/// Cookie Authentication login/logout akisini yoneten sample controller.
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
        // Login sayfasi host uygulamanin authentication sorumlulugunu gosterir.
        return View(new LoginViewModel
        {
            ReturnUrl = ReturnUrlHelper.GetSafeReturnUrl(returnUrl)
        });
    }

    [HttpPost("/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // Test users:
        // admin@runiq.dev / Password123! -> Admin role
        // user@runiq.dev  / Password123! -> No Admin role
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
        // Cookie temizlenince dashboard tekrar login ister.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return LocalRedirect("/login?ReturnUrl=/dashboard");
    }

    [HttpGet("/access-denied")]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
