namespace Runiq.AI.DashboardSecurityRole.Services;

/// <summary>
/// Login donus adresini local dashboard akisiyla sinirlar.
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
