namespace Runiq.Core.Dashboard;

/// <summary>
/// Runiq Dashboard authentication kararının türünü belirtir.
/// </summary>
public enum RuniqDashboardAuthenticationAccessMode
{
    /// <summary>
    /// Authentication kararı henüz verilmedi.
    /// </summary>
    NotConfigured = 0,

    /// <summary>
    /// Dashboard anonim erişime açıktır.
    /// </summary>
    Anonymous = 1,

    /// <summary>
    /// Dashboard sadece authenticated kullanıcıya açıktır.
    /// </summary>
    AuthenticatedUser = 2,

    /// <summary>
    /// Dashboard belirtilen rollerden en az birine sahip kullanıcıya açıktır.
    /// </summary>
    Role = 3
}
