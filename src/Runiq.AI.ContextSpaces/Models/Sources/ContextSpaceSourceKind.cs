namespace Runiq.AI.ContextSpaces.Models.Sources;

/// <summary>
/// Context space kaynaginin hangi tür bilgi kaynagini temsil ettigini belirtir.
/// </summary>
public enum ContextSpaceSourceKind
{
    /// <summary>
    /// Kaynak türünün açikça belirtilmedigini ifade eder.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Yerel dosya sistemi üzerindeki bir klasör veya dosya kaynagini ifade eder.
    /// </summary>
    LocalFileSystem = 1,

    /// <summary>
    /// Uygulama tarafindan yüklenmis dokümanlardan olusan bir kaynagi ifade eder.
    /// </summary>
    UploadedDocuments = 2,

    /// <summary>
    /// Harici veya uygulama içi veri tabani kayitlarindan olusan bir kaynagi ifade eder.
    /// </summary>
    Database = 3,

    /// <summary>
    /// Uzak nesne saklama servisindeki bir kaynagi ifade eder.
    /// </summary>
    ObjectStorage = 4,

    /// <summary>
    /// Git tabanli kaynak kodu veya dokümantasyon deposunu ifade eder.
    /// </summary>
    GitRepository = 5
}
