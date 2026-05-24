namespace Runiq.ContextSpaces.Models.Sources;

/// <summary>
/// Context space kaynağının hangi tür bilgi kaynağını temsil ettiğini belirtir.
/// </summary>
public enum ContextSpaceSourceKind
{
    /// <summary>
    /// Kaynak türünün açıkça belirtilmediğini ifade eder.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Yerel dosya sistemi üzerindeki bir klasör veya dosya kaynağını ifade eder.
    /// </summary>
    LocalFileSystem = 1,

    /// <summary>
    /// Uygulama tarafından yüklenmiş dokümanlardan oluşan bir kaynağı ifade eder.
    /// </summary>
    UploadedDocuments = 2,

    /// <summary>
    /// Harici veya uygulama içi veri tabanı kayıtlarından oluşan bir kaynağı ifade eder.
    /// </summary>
    Database = 3,

    /// <summary>
    /// Uzak nesne saklama servisindeki bir kaynağı ifade eder.
    /// </summary>
    ObjectStorage = 4,

    /// <summary>
    /// Git tabanlı kaynak kodu veya dokümantasyon deposunu ifade eder.
    /// </summary>
    GitRepository = 5
}