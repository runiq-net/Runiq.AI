using Microsoft.AspNetCore.Mvc;

namespace Runiq.AI.DashboardSecurityRole.Controllers;

/// <summary>
/// Sample baslangic adresini dashboard'a yonlendirir.
/// </summary>
public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return LocalRedirect("/dashboard");
    }
}
