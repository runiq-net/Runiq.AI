using Microsoft.AspNetCore.Mvc;

namespace Runiq.DashboardSecurityUser.Controllers;

/// <summary>
/// Sample başlangıç adresini dashboard'a yönlendirir.
/// </summary>
public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return LocalRedirect("/dashboard");
    }
}
