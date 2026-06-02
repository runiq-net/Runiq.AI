namespace Runiq.Core.Dashboard;

/// <summary>
/// Runiq Dashboard authentication kararını yapılandırır.
/// </summary>
public sealed class RuniqDashboardAuthenticationOptions
{
    private readonly List<string> roles = [];

    /// <summary>
    /// Dashboard authentication kararının türünü döndürür.
    /// </summary>
    public RuniqDashboardAccessMode AccessMode { get; private set; } =
        RuniqDashboardAccessMode.NotConfigured;

    /// <summary>
    /// Role tabanlı erişimde kabul edilen rolleri döndürür.
    /// </summary>
    public IReadOnlyList<string> Roles => roles;

    /// <summary>
    /// Dashboard'u anonim erişime açar.
    /// </summary>
    public void AllowAnonymous()
    {
        EnsureNotConfigured();

        AccessMode = RuniqDashboardAccessMode.Anonymous;
    }

    /// <summary>
    /// Dashboard'u authenticated kullanıcılarla sınırlar.
    /// </summary>
    public void RequireAuthenticatedUser()
    {
        EnsureNotConfigured();

        AccessMode = RuniqDashboardAccessMode.AuthenticatedUser;
    }

    /// <summary>
    /// Dashboard'u belirtilen rollerden en az birine sahip kullanıcılarla sınırlar.
    /// </summary>
    /// <param name="roles">Erişime izin verilecek roller.</param>
    public void RequireRole(params string[] roles)
    {
        EnsureNotConfigured();
        ArgumentNullException.ThrowIfNull(roles);

        var normalizedRoles = roles
            .Select(role =>
            {
                if (string.IsNullOrWhiteSpace(role))
                {
                    throw new ArgumentException(
                        "Dashboard authentication roles cannot contain null, empty, or whitespace values.",
                        nameof(roles));
                }

                return role.Trim();
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedRoles.Length == 0)
        {
            throw new ArgumentException(
                "Dashboard authentication requires at least one role.",
                nameof(roles));
        }

        this.roles.AddRange(normalizedRoles);
        AccessMode = RuniqDashboardAccessMode.Role;
    }

    private void EnsureNotConfigured()
    {
        if (AccessMode != RuniqDashboardAccessMode.NotConfigured)
        {
            throw new InvalidOperationException(
                "Dashboard authentication has already been configured. " +
                "Use only one of auth.RequireRole(...), auth.RequireAuthenticatedUser(), or auth.AllowAnonymous().");
        }
    }
}
