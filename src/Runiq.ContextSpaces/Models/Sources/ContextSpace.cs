using Runiq.ContextSpaces.Builders;
using Runiq.ContextSpaces.Models.Skills;

namespace Runiq.ContextSpaces.Models.Sources;

/// <summary>
/// Bir agent'ın görev yaparken erişebileceği bilgi, kaynak ve politika sınırını temsil eder.
/// </summary>
public sealed class ContextSpace
{
    private readonly List<ContextSpaceSource> _sources = [];

    private readonly List<ContextSpaceSkillSource> skillSources = [];

    /// <summary>
    /// Context space için benzersiz teknik kimliği ifade eder.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Dashboard ve metadata çıktılarında gösterilecek okunabilir adı ifade eder.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Context space'in amacını açıklayan isteğe bağlı metni ifade eder.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Context space içinde tanımlı bilgi kaynaklarını ifade eder.
    /// </summary>
    public IReadOnlyList<ContextSpaceSource> Sources => _sources;

    /// <summary>
    /// Bağlam alanına bağlı skill kaynaklarını döner.
    /// </summary>
    public IReadOnlyList<ContextSpaceSkillSource> SkillSources => skillSources;

    /// <summary>
    /// Yeni bir context space örneği oluşturur.
    /// </summary>
    /// <param name="id">Context space için benzersiz teknik kimlik.</param>
    /// <param name="name">Context space için okunabilir ad.</param>
    /// <param name="description">Context space'in isteğe bağlı açıklaması.</param>
    public ContextSpace(
        string id,
        string name,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Context space id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Context space name cannot be empty.", nameof(name));
        }

        Id = id.Trim();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
    }

    /// <summary>
    /// Context space'e yeni bir bilgi kaynağı ekler.
    /// </summary>
    /// <param name="source">Eklenecek bilgi kaynağı.</param>
    /// <returns>Akıcı yapılandırma için mevcut context space örneği.</returns>
    public ContextSpace AddSource(ContextSpaceSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (_sources.Any(existing =>
                string.Equals(existing.Id, source.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"A context space source with id '{source.Id}' is already registered.");
        }

        _sources.Add(source);
        return this;
    }

    /// <summary>
    /// Bağlam alanına skill kaynakları ekler.
    /// </summary>
    public ContextSpace AddSkills(Action<ContextSpaceSkillBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ContextSpaceSkillBuilder();
        configure(builder);

        foreach (var skillSource in builder.Build())
        {
            AddSkillSource(skillSource);
        }

        return this;
    }

    private void AddSkillSource(ContextSpaceSkillSource skillSource)
    {
        ArgumentNullException.ThrowIfNull(skillSource);

        if (skillSources.Any(existing =>
            string.Equals(existing.Id, skillSource.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Context space '{Id}' already contains a skill source with id '{skillSource.Id}'.");
        }

        skillSources.Add(skillSource);
    }

    /// <summary>
    /// Context space'e bilgi kaynakları ekler.
    /// </summary>
    public ContextSpace AddSources(Action<ContextSpaceSourceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ContextSpaceSourceBuilder();
        configure(builder);

        foreach (var source in builder.Build())
        {
            AddSource(source);
        }

        return this;
    }

}