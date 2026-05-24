namespace Runiq.ContextSpaces.Models.Skills;

/// <summary>
/// Bağlam alanı kaynaklarının veya skill kaynaklarının hangi ortamdan okunacağını belirtir.
/// </summary>
public enum ContextSpaceLocationKind
{
    /// <summary>
    /// Tanımsız konum türünü ifade eder.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Yerel dosya sistemi üzerinden erişilen konumu ifade eder.
    /// </summary>
    FileSystem = 1,

    /// <summary>
    /// S3 uyumlu object storage üzerinden erişilen konumu ifade eder.
    /// </summary>
    S3 = 2,
}