using Runiq.AI.ContextSpaces.Models.Sources;
using Runiq.AI.ContextSpaces.Services;

namespace Runiq.AI.ContextSpaces.Tests;

public sealed class ContextSpaceSkillDiscoveryServiceTests : IDisposable
{
    private readonly string temporaryDirectory;

    public ContextSpaceSkillDiscoveryServiceTests()
    {
        temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"runiq-skills-{Guid.NewGuid():N}");

        Directory.CreateDirectory(temporaryDirectory);
    }

    [Fact]
    public void Discover_ShouldLoadSkillsFromFileSystemSkillSource()
    {
        // Bu test, dosya sistemi tabanli skill kaynagindan SKILL.md dosyalarinin kesfedilebildigini dogrular.
        var skillDirectory = Path.Combine(temporaryDirectory, "trip-planning");
        Directory.CreateDirectory(skillDirectory);

        File.WriteAllText(
            Path.Combine(skillDirectory, "SKILL.md"),
            """
            ---
            name: trip-planning
            description: Guides the agent when creating short city trip plans.
            version: 1.0.0
            tags:
              - travel
              - planning
            ---

            # Trip Planning

            Prefer realistic and short city itineraries.
            """);

        var contextSpace = new ContextSpace(
                id: "travel-planning",
                name: "Travel Planning")
            .AddSkills(skills => skills.FromFileSystem(
                id: "travel-skills",
                name: "Travel Skills",
                path: temporaryDirectory));

        var discoveryService = new ContextSpaceSkillDiscoveryService();

        var discoveredSkills = discoveryService.Discover(contextSpace);

        var skill = Assert.Single(discoveredSkills);

        Assert.Equal("trip-planning", skill.Id);
        Assert.Equal("trip-planning", skill.Name);
        Assert.Equal("Guides the agent when creating short city trip plans.", skill.Description);
        Assert.Equal("1.0.0", skill.Version);
        Assert.Equal(["travel", "planning"], skill.Tags);
        Assert.Equal("travel-skills", skill.SourceId);
        Assert.Equal("trip-planning/SKILL.md", skill.RelativePath);
        Assert.Contains("# Trip Planning", skill.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Discover_ShouldReturnEmptyListWhenFileSystemPathDoesNotExist()
    {
        // Bu test, mevcut olmayan dosya sistemi skill kaynaginin bos sonuç döndürdügünü dogrular.
        var contextSpace = new ContextSpace(
                id: "travel-planning",
                name: "Travel Planning")
            .AddSkills(skills => skills.FromFileSystem(
                id: "travel-skills",
                name: "Travel Skills",
                path: Path.Combine(temporaryDirectory, "missing")));

        var discoveryService = new ContextSpaceSkillDiscoveryService();

        var discoveredSkills = discoveryService.Discover(contextSpace);

        Assert.Empty(discoveredSkills);
    }

    [Fact]
    public void Discover_ShouldIgnoreS3SkillSourcesForNow()
    {
        // Bu test, ilk asamada S3 skill kaynaklarinin metadata olarak kayitli kalip kesif üretmedigini dogrular.
        var contextSpace = new ContextSpace(
                id: "travel-planning",
                name: "Travel Planning")
            .AddSkills(skills => skills.FromS3(
                id: "team-skills",
                name: "Team Skills",
                bucketName: "runiq-contexts",
                prefix: "travel-planning/skills"));

        var discoveryService = new ContextSpaceSkillDiscoveryService();

        var discoveredSkills = discoveryService.Discover(contextSpace);

        Assert.Empty(discoveredSkills);
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
