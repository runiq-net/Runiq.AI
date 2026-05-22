namespace Runiq.Core.Studio;

/// <summary>
/// Runiq Dashboard statik dosyalarını assembly embedded resource kaynaklarından okur.
/// </summary>
internal static class RuniqDashboardAssets
{
    private const string ResourcePrefix = "Runiq.Core.Studio.wwwroot.";

    /// <summary>
    /// Embedded dashboard asset dosyasını stream olarak açar.
    /// </summary>
    /// <param name="relativePath">wwwroot altındaki göreli dosya yoludur.</param>
    /// <returns>Asset stream'i; bulunamazsa null döner.</returns>
    public static Stream? OpenRead(string relativePath)
    {
        var resourceName = ToResourceName(relativePath);

        return typeof(RuniqDashboardAssets)
            .Assembly
            .GetManifestResourceStream(resourceName);
    }

    /// <summary>
    /// Embedded dashboard asset dosyasını metin olarak okur.
    /// </summary>
    /// <param name="relativePath">wwwroot altındaki göreli dosya yoludur.</param>
    /// <param name="cancellationToken">İptal bildirimidir.</param>
    /// <returns>Asset metin içeriğidir.</returns>
    public static async Task<string?> ReadTextAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = OpenRead(relativePath);

        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// Göreli asset yolunu embedded resource adına dönüştürür.
    /// </summary>
    /// <param name="relativePath">wwwroot altındaki göreli dosya yoludur.</param>
    /// <returns>Manifest resource adıdır.</returns>
    private static string ToResourceName(string relativePath)
    {
        var normalizedPath = relativePath
            .TrimStart('/', '\\')
            .Replace('\\', '/');

        var resourceSuffix = normalizedPath.Replace('/', '.');

        return ResourcePrefix + resourceSuffix;
    }
}