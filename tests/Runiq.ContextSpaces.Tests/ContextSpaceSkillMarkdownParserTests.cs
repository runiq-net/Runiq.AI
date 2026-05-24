using Runiq.ContextSpaces.Services;

namespace Runiq.ContextSpaces.Tests;

public sealed class ContextSpaceSkillMarkdownParserTests
{
    [Fact]
    public void Parse_ShouldCreateSkillFromSkillMarkdown()
    {
        // Bu test, geçerli SKILL.md içeriğinin skill modeline dönüştürülebildiğini doğrular.
        const string content = """
            ---
            name: code-review
            description: Reviews code for quality, style, and potential issues
            version: 1.0.0
            tags:
              - development
              - review
            ---

            # Code Review

            When reviewing code:

            1. Check for bugs and edge cases.
            2. Verify the code follows the style guide.
            """;

        var parser = new ContextSpaceSkillMarkdownParser();

        var skill = parser.Parse(
            content: content,
            sourceId: "project-skills",
            relativePath: "code-review/SKILL.md");

        Assert.Equal("code-review", skill.Id);
        Assert.Equal("code-review", skill.Name);
        Assert.Equal("Reviews code for quality, style, and potential issues", skill.Description);
        Assert.Equal("1.0.0", skill.Version);
        Assert.Equal(["development", "review"], skill.Tags);
        Assert.Equal("project-skills", skill.SourceId);
        Assert.Equal("code-review/SKILL.md", skill.RelativePath);
        Assert.Contains("# Code Review", skill.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ShouldRejectMissingFrontMatter()
    {
        // Bu test, front matter içermeyen SKILL.md içeriğinin kabul edilmediğini doğrular.
        const string content = """
            # Code Review

            Missing front matter.
            """;

        var parser = new ContextSpaceSkillMarkdownParser();

        var exception = Assert.Throws<FormatException>(() =>
            parser.Parse(
                content: content,
                sourceId: "project-skills",
                relativePath: "code-review/SKILL.md"));

        Assert.Equal(
            "SKILL.md content must start with YAML front matter.",
            exception.Message);
    }

    [Fact]
    public void Parse_ShouldRejectMissingName()
    {
        // Bu test, name alanı olmayan SKILL.md içeriğinin kabul edilmediğini doğrular.
        const string content = """
            ---
            description: Reviews code.
            ---

            # Code Review
            """;

        var parser = new ContextSpaceSkillMarkdownParser();

        var exception = Assert.Throws<FormatException>(() =>
            parser.Parse(
                content: content,
                sourceId: "project-skills",
                relativePath: "code-review/SKILL.md"));

        Assert.Equal(
            "SKILL.md front matter must contain 'name'.",
            exception.Message);
    }

    [Fact]
    public void Parse_ShouldRejectEmptyInstructions()
    {
        // Bu test, front matter sonrası yönerge içeriği boş olan SKILL.md dosyasının kabul edilmediğini doğrular.
        const string content = """
            ---
            name: code-review
            ---
            """;

        var parser = new ContextSpaceSkillMarkdownParser();

        var exception = Assert.Throws<FormatException>(() =>
            parser.Parse(
                content: content,
                sourceId: "project-skills",
                relativePath: "code-review/SKILL.md"));

        Assert.Equal(
            "SKILL.md front matter closing delimiter was not found.",
            exception.Message);
    }
}