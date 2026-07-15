using Runiq.AI.ContextSpaces.Models.Skills;

namespace Runiq.AI.ContextSpaces.Models.Skills;

/// <summary>
/// Bir baglam alani içinde skill dosyalarinin nereden kesfedilecegini tanimlar.
/// </summary>
public sealed class ContextSpaceSkillSource
{
    private ContextSpaceSkillSource(
        string id,
        string name,
        ContextSpaceLocationKind kind,
        string? path,
        string? bucketName,
        string? prefix)
    {
        Id = ValidateRequired(id, nameof(id));
        Name = ValidateRequired(name, nameof(name));
        Kind = kind;
        Path = NormalizeOptional(path);
        BucketName = NormalizeOptional(bucketName);
        Prefix = NormalizeOptional(prefix);

        ValidateLocation();
    }

    /// <summary>
    /// Skill kaynaginin baglam alani içindeki benzersiz kimligini döner.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Skill kaynaginin kullaniciya gösterilecek adini döner.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Skill kaynaginin hangi ortamdan okunacagini döner.
    /// </summary>
    public ContextSpaceLocationKind Kind { get; }

    /// <summary>
    /// Yerel dosya sistemi skill kaynagi için kök klasör yolunu döner.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// S3 skill kaynagi için bucket adini döner.
    /// </summary>
    public string? BucketName { get; }

    /// <summary>
    /// S3 skill kaynagi için prefix degerini döner.
    /// </summary>
    public string? Prefix { get; }

    /// <summary>
    /// Yerel dosya sisteminden okunacak bir skill kaynagi olusturur.
    /// </summary>
    public static ContextSpaceSkillSource FileSystem(
        string id,
        string name,
        string path)
    {
        return new ContextSpaceSkillSource(
            id: id,
            name: name,
            kind: ContextSpaceLocationKind.FileSystem,
            path: path,
            bucketName: null,
            prefix: null);
    }

    /// <summary>
    /// S3 uyumlu object storage üzerinden okunacak bir skill kaynagi olusturur.
    /// </summary>
    public static ContextSpaceSkillSource S3(
        string id,
        string name,
        string bucketName,
        string prefix)
    {
        return new ContextSpaceSkillSource(
            id: id,
            name: name,
            kind: ContextSpaceLocationKind.S3,
            path: null,
            bucketName: bucketName,
            prefix: prefix);
    }

    private void ValidateLocation()
    {
        switch (Kind)
        {
            case ContextSpaceLocationKind.FileSystem:
                _ = ValidateRequired(Path, nameof(Path));
                break;

            case ContextSpaceLocationKind.S3:
                _ = ValidateRequired(BucketName, nameof(BucketName));
                _ = ValidateRequired(Prefix, nameof(Prefix));
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(Kind),
                    Kind,
                    "Skill source location kind is not supported.");
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
