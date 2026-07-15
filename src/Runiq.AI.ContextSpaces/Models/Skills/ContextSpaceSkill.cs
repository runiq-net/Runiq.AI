namespace Runiq.AI.ContextSpaces.Models.Skills;

/// <summary>
/// Bir baglam alani içinde kesfedilen tekil skill tanimini temsil eder.
/// </summary>
public sealed class ContextSpaceSkill
{
    /// <summary>
    /// Yeni bir skill tanimi olusturur.
    /// </summary>
    public ContextSpaceSkill(
        string id,
        string name,
        string? description,
        string? version,
        IEnumerable<string>? tags,
        string instructions,
        string sourceId,
        string relativePath)
    {
        Id = ValidateRequired(id, nameof(id));
        Name = ValidateRequired(name, nameof(name));
        Description = NormalizeOptional(description);
        Version = NormalizeOptional(version);
        Tags = NormalizeTags(tags);
        Instructions = ValidateRequired(instructions, nameof(instructions));
        SourceId = ValidateRequired(sourceId, nameof(sourceId));
        RelativePath = ValidateRequired(relativePath, nameof(relativePath));
    }

    /// <summary>
    /// Skill taniminin benzersiz kimligini döner.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Skill taniminin kullaniciya gösterilecek adini döner.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Skill taniminin açiklamasini döner.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Skill taniminin sürüm bilgisini döner.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Skill tanimina ait etiketleri döner.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Skill taniminin agent tarafindan kullanilabilecek yönerge içerigini döner.
    /// </summary>
    public string Instructions { get; }

    /// <summary>
    /// Skill taniminin kesfedildigi skill kaynaginin kimligini döner.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Skill taniminin skill kaynagina göre göreli dosya yolunu döner.
    /// </summary>
    public string RelativePath { get; }

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

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
