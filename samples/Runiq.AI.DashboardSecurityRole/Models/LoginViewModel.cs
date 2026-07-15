using System.ComponentModel.DataAnnotations;

namespace Runiq.AI.DashboardSecurityRole.Models;

/// <summary>
/// Login formundan gelen sample kullanici bilgisini tasir.
/// </summary>
public sealed class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "admin@runiq.dev";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = "/dashboard";
}
