namespace Runiq.AI.ContextSpaces.Models.Skills;

/// <summary>
/// Baglam alani kaynaklarinin veya skill kaynaklarinin hangi ortamdan okunacagini belirtir.
/// </summary>
public enum ContextSpaceLocationKind
{
    /// <summary>
    /// Tanimsiz konum türünü ifade eder.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Yerel dosya sistemi üzerinden erisilen konumu ifade eder.
    /// </summary>
    FileSystem = 1,

    /// <summary>
    /// S3 uyumlu object storage üzerinden erisilen konumu ifade eder.
    /// </summary>
    S3 = 2,
}
