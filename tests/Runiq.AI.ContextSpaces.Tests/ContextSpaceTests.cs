using Runiq.AI.ContextSpaces.Models.Skills;
using Runiq.AI.ContextSpaces.Models.Sources;

namespace Runiq.AI.ContextSpaces.Tests;

public sealed class ContextSpaceTests
{
    [Fact]
    public void Constructor_ShouldTrimIdNameAndDescription()
    {
        // ContextSpace olusturulurken temel metin alanlarinin normalize edildigini dogrular.
        var contextSpace = new ContextSpace(
            id: " travel-planning ",
            name: " Travel Planning ",
            description: " Shared travel context ");

        Assert.Equal("travel-planning", contextSpace.Id);
        Assert.Equal("Travel Planning", contextSpace.Name);
        Assert.Equal("Shared travel context", contextSpace.Description);
    }

    [Fact]
    public void Constructor_ShouldSetDescriptionToNull_WhenDescriptionIsEmpty()
    {
        // Bos açiklama verildiginde metadata çiktisinda gereksiz bos metin tasinmadigini dogrular.
        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning",
            description: " ");

        Assert.Null(contextSpace.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_ShouldThrow_WhenIdIsEmpty(string id)
    {
        // ContextSpace teknik kimliginin bos birakilamayacagini dogrular.
        var exception = Assert.Throws<ArgumentException>(() =>
            new ContextSpace(id, "Travel Planning"));

        Assert.Equal("id", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_ShouldThrow_WhenNameIsEmpty(string name)
    {
        // ContextSpace görünen adinin bos birakilamayacagini dogrular.
        var exception = Assert.Throws<ArgumentException>(() =>
            new ContextSpace("travel-planning", name));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void AddSource_ShouldAddSource()
    {
        // ContextSpace içine bilgi kaynagi eklenebildigini dogrular.
        var contextSpace = new ContextSpace("travel-planning", "Travel Planning");

        contextSpace.AddSource(new ContextSpaceSource(
            id: "travel-docs",
            name: "Travel Documents",
            kind: ContextSpaceSourceKind.UploadedDocuments));

        var source = Assert.Single(contextSpace.Sources);
        Assert.Equal("travel-docs", source.Id);
        Assert.Equal("Travel Documents", source.Name);
        Assert.Equal(ContextSpaceSourceKind.UploadedDocuments, source.Kind);
    }

    [Fact]
    public void AddSource_ShouldThrow_WhenSourceIdAlreadyExistsIgnoringCase()
    {
        // Ayni ContextSpace içinde kaynak id tekrarinin case-insensitive engellendigini dogrular.
        var contextSpace = new ContextSpace("travel-planning", "Travel Planning");

        contextSpace.AddSource(new ContextSpaceSource(
            id: "travel-docs",
            name: "Travel Documents",
            kind: ContextSpaceSourceKind.UploadedDocuments));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            contextSpace.AddSource(new ContextSpaceSource(
                id: "TRAVEL-DOCS",
                name: "Other Travel Documents",
                kind: ContextSpaceSourceKind.UploadedDocuments)));

        Assert.Contains("travel-docs", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddSkills_ShouldRegisterFileSystemSkillSource()
    {
        // Bu test, baglam alanina dosya sistemi tabanli skill kaynagi eklenebildigini dogrular.
        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSkills(skills => skills.FromFileSystem(
            id: "travel-skills",
            name: "Travel Skills",
            path: "./Contexts/TravelPlanning/skills"));

        var skillSource = Assert.Single(contextSpace.SkillSources);

        Assert.Equal("travel-skills", skillSource.Id);
        Assert.Equal("Travel Skills", skillSource.Name);
        Assert.Equal(ContextSpaceLocationKind.FileSystem, skillSource.Kind);
        Assert.Equal("./Contexts/TravelPlanning/skills", skillSource.Path);
        Assert.Null(skillSource.BucketName);
        Assert.Null(skillSource.Prefix);
    }

    [Fact]
    public void AddSkills_ShouldRegisterS3SkillSource()
    {
        // Bu test, baglam alanina S3 tabanli skill kaynagi metadata'si eklenebildigini dogrular.
        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSkills(skills => skills.FromS3(
            id: "team-skills",
            name: "Team Skills",
            bucketName: "runiq-contexts",
            prefix: "travel-planning/skills"));

        var skillSource = Assert.Single(contextSpace.SkillSources);

        Assert.Equal("team-skills", skillSource.Id);
        Assert.Equal("Team Skills", skillSource.Name);
        Assert.Equal(ContextSpaceLocationKind.S3, skillSource.Kind);
        Assert.Null(skillSource.Path);
        Assert.Equal("runiq-contexts", skillSource.BucketName);
        Assert.Equal("travel-planning/skills", skillSource.Prefix);
    }

    [Fact]
    public void AddSkills_ShouldRejectDuplicateSkillSourceIds()
    {
        // Bu test, ayni baglam alani içinde skill kaynagi kimliginin tekrar kullanilamayacagini dogrular.
        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSkills(skills => skills.FromFileSystem(
            id: "travel-skills",
            name: "Travel Skills",
            path: "./Contexts/TravelPlanning/skills"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            contextSpace.AddSkills(skills => skills.FromS3(
                id: "TRAVEL-SKILLS",
                name: "Team Skills",
                bucketName: "runiq-contexts",
                prefix: "travel-planning/skills")));

        Assert.Equal(
            "Context space 'travel-planning' already contains a skill source with id 'TRAVEL-SKILLS'.",
            exception.Message);
    }

    [Fact]
    public void AddSources_ShouldRegisterFileSystemSource()
    {
        // Bu test, baglam alanina dosya sistemi tabanli bilgi kaynagi eklenebildigini dogrular.
        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSources(sources => sources.FromFileSystem(
            id: "travel-docs",
            name: "Travel Documents",
            path: "./Contexts/TravelPlanning/sources",
            description: "Sample travel planning documents."));

        var source = Assert.Single(contextSpace.Sources);

        Assert.Equal("travel-docs", source.Id);
        Assert.Equal("Travel Documents", source.Name);
        Assert.Equal(ContextSpaceSourceKind.LocalFileSystem, source.Kind);
        Assert.Equal("Sample travel planning documents.", source.Description);
        Assert.Equal("./Contexts/TravelPlanning/sources", source.Path);
        Assert.Null(source.BucketName);
        Assert.Null(source.Prefix);
    }

    [Fact]
    public void AddSources_ShouldRegisterS3Source()
    {
        // Bu test, baglam alanina S3 tabanli bilgi kaynagi metadata'si eklenebildigini dogrular.
        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSources(sources => sources.FromS3(
            id: "travel-docs",
            name: "Travel Documents",
            bucketName: "runiq-contexts",
            prefix: "travel-planning/sources",
            description: "Sample travel planning documents."));

        var source = Assert.Single(contextSpace.Sources);

        Assert.Equal("travel-docs", source.Id);
        Assert.Equal("Travel Documents", source.Name);
        Assert.Equal(ContextSpaceSourceKind.ObjectStorage, source.Kind);
        Assert.Equal("Sample travel planning documents.", source.Description);
        Assert.Null(source.Path);
        Assert.Equal("runiq-contexts", source.BucketName);
        Assert.Equal("travel-planning/sources", source.Prefix);
    }

    [Fact]
    public void AddSources_ShouldRejectDuplicateSourceIds()
    {
        // Bu test, ayni baglam alani içinde bilgi kaynagi kimliginin tekrar kullanilamayacagini dogrular.
        var contextSpace = new ContextSpace(
            id: "travel-planning",
            name: "Travel Planning");

        contextSpace.AddSources(sources => sources.FromFileSystem(
            id: "travel-docs",
            name: "Travel Documents",
            path: "./Contexts/TravelPlanning/sources"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            contextSpace.AddSources(sources => sources.FromS3(
                id: "TRAVEL-DOCS",
                name: "Travel Documents",
                bucketName: "runiq-contexts",
                prefix: "travel-planning/sources")));

        Assert.Equal(
            "A context space source with id 'TRAVEL-DOCS' is already registered.",
            exception.Message);
    }

}
