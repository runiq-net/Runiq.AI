namespace Runiq.Core.Dashboard;

/// <summary>
/// Runiq Dashboard endpoint publishing options for the host application.
/// </summary>
public sealed class RuniqDashboardOptions
{
    private readonly RuniqDashboardAuthenticationOptions authentication = new();

    /// <summary>
    /// Base path where the dashboard is published. Defaults to "/runiq".
    /// </summary>
    public string Path { get; set; } = "/runiq";

    /// <summary>
    /// Title displayed in the dashboard application. Defaults to "Runiq Dashboard".
    /// </summary>
    public string Title { get; set; } = "Runiq Dashboard";

    /// <summary>
    /// Dashboard authentication ayarlarını döndürür.
    /// </summary>
    public RuniqDashboardAuthenticationOptions AuthenticationOptions => authentication;

    /// <summary>
    /// Dashboard authentication kararını yapılandırır.
    /// </summary>
    /// <param name="configure">Authentication ayarlarını yapılandıran callback.</param>
    public void Authentication(Action<RuniqDashboardAuthenticationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(authentication);
    }

    internal void ValidateAuthentication()
    {
        if (authentication.AccessMode == RuniqDashboardAuthenticationAccessMode.NotConfigured)
        {
            throw new InvalidOperationException(
                "Runiq Dashboard authentication must be explicitly configured when UseRuniqDashboard(...) is used. " +
                "Configure it with auth.RequireRole(...), auth.RequireAuthenticatedUser(), or auth.AllowAnonymous().");
        }
    }
}
