using Runiq.ContextSpaces.Models.Skills;
using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.ContextSpaces.Services;

/// <summary>
/// Bağlam alanına bağlı skill kaynaklarından SKILL.md dosyalarını keşfeder.
/// </summary>
public sealed class ContextSpaceSkillDiscoveryService : IContextSpaceSkillDiscoveryService
{
    private const string SkillFileName = "SKILL.md";

    private readonly ContextSpaceSkillMarkdownParser parser;

    /// <summary>
    /// Yeni bir skill keşif servisi oluşturur.
    /// </summary>
    public ContextSpaceSkillDiscoveryService()
        : this(new ContextSpaceSkillMarkdownParser())
    {
    }

    /// <summary>
    /// Yeni bir skill keşif servisi oluşturur.
    /// </summary>
    public ContextSpaceSkillDiscoveryService(ContextSpaceSkillMarkdownParser parser)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <inheritdoc />
    public IReadOnlyList<ContextSpaceSkill> Discover(ContextSpace contextSpace)
    {
        ArgumentNullException.ThrowIfNull(contextSpace);

        var discoveredSkills = new List<ContextSpaceSkill>();

        foreach (var skillSource in contextSpace.SkillSources)
        {
            if (skillSource.Kind != ContextSpaceLocationKind.FileSystem)
            {
                continue;
            }

            discoveredSkills.AddRange(DiscoverFromFileSystem(skillSource));
        }

        return discoveredSkills;
    }

    private IReadOnlyList<ContextSpaceSkill> DiscoverFromFileSystem(
        ContextSpaceSkillSource skillSource)
    {
        if (string.IsNullOrWhiteSpace(skillSource.Path))
        {
            return [];
        }

        var rootPath = Path.GetFullPath(skillSource.Path);

        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        var skillFiles = Directory
            .EnumerateFiles(rootPath, SkillFileName, SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase);

        var discoveredSkills = new List<ContextSpaceSkill>();

        foreach (var skillFile in skillFiles)
        {
            var relativePath = Path
                .GetRelativePath(rootPath, skillFile)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

            var content = File.ReadAllText(skillFile);
            var skill = parser.Parse(
                content: content,
                sourceId: skillSource.Id,
                relativePath: relativePath);

            discoveredSkills.Add(skill);
        }

        return discoveredSkills;
    }
}