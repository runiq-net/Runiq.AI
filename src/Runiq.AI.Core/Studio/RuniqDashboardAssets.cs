namespace Runiq.AI.Core.Studio;

/// <summary>
/// Runiq Dashboard statik dosyalarini assembly embedded resource kaynaklarindan okur.
/// </summary>
internal static class RuniqDashboardAssets
{
    private const string ResourcePrefix = "Runiq.AI.Core.Studio.wwwroot.";

    /// <summary>
    /// Embedded dashboard asset dosyasini stream olarak açar.
    /// </summary>
    /// <param name="relativePath">wwwroot altindaki göreli dosya yoludur.</param>
    /// <returns>Asset stream'i; bulunamazsa null döner.</returns>
    public static Stream? OpenRead(string relativePath)
    {
        var resourceName = ToResourceName(relativePath);

        return typeof(RuniqDashboardAssets)
            .Assembly
            .GetManifestResourceStream(resourceName);
    }

    /// <summary>
    /// Embedded dashboard asset dosyasini metin olarak okur.
    /// </summary>
    /// <param name="relativePath">wwwroot altindaki göreli dosya yoludur.</param>
    /// <param name="cancellationToken">Iptal bildirimidir.</param>
    /// <returns>Asset metin içerigidir.</returns>
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
    /// Göreli asset yolunu embedded resource adina dönüstürür.
    /// </summary>
    /// <param name="relativePath">wwwroot altindaki göreli dosya yoludur.</param>
    /// <returns>Manifest resource adidir.</returns>
    private static string ToResourceName(string relativePath)
    {
        var normalizedPath = relativePath
            .TrimStart('/', '\\')
            .Replace('\\', '/');

        var resourceSuffix = normalizedPath.Replace('/', '.');

        return ResourcePrefix + resourceSuffix;
    }
}
