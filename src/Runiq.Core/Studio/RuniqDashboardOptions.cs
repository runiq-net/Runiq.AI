namespace Runiq.Core.Dashboard;

/// <summary>
/// Runiq Dashboard uç noktasının host uygulama içinde nasıl yayınlanacağını belirleyen ayarlardır.
/// </summary>
public sealed class RuniqDashboardOptions
{
    /// <summary>
    /// Dashboard'un yayınlanacağı temel path değeridir.
    /// Varsayılan değer "/runiq" olur.
    /// </summary>
    public string Path { get; set; } = "/runiq";

    /// <summary>
    /// Dashboard uygulamasında gösterilecek başlık bilgisidir.
    /// Varsayılan değer "Runiq Dashboard" olur.
    /// </summary>
    public string Title { get; set; } = "Runiq Dashboard";

    /// <summary>
    /// Dashboard API ve metadata endpoint'lerini korumak için kullanılan API anahtarıdır.
    /// Ayarlandığında, tüm <c>/api/</c> ve <c>/metadata/</c> istekleri
    /// <c>Authorization: Bearer {key}</c> veya <c>X-Api-Key: {key}</c> header'ı gerektirir.
    /// <c>null</c> olduğunda kimlik doğrulama uygulanmaz (varsayılan).
    /// </summary>
    public string? ApiKey { get; set; }
}