using System.ComponentModel.DataAnnotations;

namespace Runiq.DashboardSecurityUser.Models;

/// <summary>
/// Login formundan gelen sample kullanıcı bilgisini taşır.
/// </summary>
public sealed class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "user@runiq.dev";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = "/dashboard";
}
