namespace Runiq.ContextSpaces.Models.Sources;

/// <summary>
/// Context space içinde agent'ın erişebileceği bir bilgi kaynağını temsil eder.
/// </summary>
public sealed class ContextSpaceSource
{
    /// <summary>
    /// Yeni bir context space source örneği oluşturur.
    /// </summary>
    public ContextSpaceSource(
        string id,
        string name,
        ContextSpaceSourceKind kind,
        string? description = null)
        : this(
            id: id,
            name: name,
            kind: kind,
            description: description,
            path: null,
            bucketName: null,
            prefix: null)
    {
    }

    private ContextSpaceSource(
        string id,
        string name,
        ContextSpaceSourceKind kind,
        string? description,
        string? path,
        string? bucketName,
        string? prefix)
    {
        Id = ValidateRequired(id, nameof(id));
        Name = ValidateRequired(name, nameof(name));
        Kind = kind;
        Description = NormalizeOptional(description);
        Path = NormalizeOptional(path);
        BucketName = NormalizeOptional(bucketName);
        Prefix = NormalizeOptional(prefix);

        ValidateLocation();
    }

    /// <summary>
    /// Kaynak için benzersiz teknik kimliği ifade eder.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Dashboard ve metadata çıktılarında gösterilecek okunabilir kaynak adını ifade eder.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Kaynağın türünü ifade eder.
    /// </summary>
    public ContextSpaceSourceKind Kind { get; }

    /// <summary>
    /// Kaynağın okunabilir açıklamasını ifade eder.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Yerel dosya sistemi kaynağı için kök klasör yolunu döner.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// S3 uyumlu object storage kaynağı için bucket adını döner.
    /// </summary>
    public string? BucketName { get; }

    /// <summary>
    /// S3 uyumlu object storage kaynağı için prefix değerini döner.
    /// </summary>
    public string? Prefix { get; }

    /// <summary>
    /// Yerel dosya sisteminden okunacak bir context source oluşturur.
    /// </summary>
    public static ContextSpaceSource FileSystem(
        string id,
        string name,
        string path,
        string? description = null)
    {
        return new ContextSpaceSource(
            id: id,
            name: name,
            kind: ContextSpaceSourceKind.LocalFileSystem,
            description: description,
            path: path,
            bucketName: null,
            prefix: null);
    }

    /// <summary>
    /// S3 uyumlu object storage üzerinden okunacak bir context source oluşturur.
    /// </summary>
    public static ContextSpaceSource S3(
        string id,
        string name,
        string bucketName,
        string prefix,
        string? description = null)
    {
        return new ContextSpaceSource(
            id: id,
            name: name,
            kind: ContextSpaceSourceKind.ObjectStorage,
            description: description,
            path: null,
            bucketName: bucketName,
            prefix: prefix);
    }

    private void ValidateLocation()
    {
        switch (Kind)
        {
            case ContextSpaceSourceKind.LocalFileSystem:
                _ = ValidateRequired(Path, nameof(Path));
                break;

            case ContextSpaceSourceKind.ObjectStorage:
                _ = ValidateRequired(BucketName, nameof(BucketName));
                _ = ValidateRequired(Prefix, nameof(Prefix));
                break;

            case ContextSpaceSourceKind.Unknown:
            case ContextSpaceSourceKind.UploadedDocuments:
            case ContextSpaceSourceKind.Database:
            case ContextSpaceSourceKind.GitRepository:
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(Kind),
                    Kind,
                    "Context source kind is not supported.");
        }
    }

    private static string ValidateRequired(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value cannot be null, empty, or whitespace.",
                parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}