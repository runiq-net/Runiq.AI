namespace Runiq.AI.DashboardSecurityUser.Services;

/// <summary>
/// Login dönüş adresini local dashboard akışıyla sınırlar.
/// </summary>
public static class ReturnUrlHelper
{
    public static string GetSafeReturnUrl(string? returnUrl)
    {
        return string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/')
            ? "/dashboard"
            : returnUrl;
    }
}
