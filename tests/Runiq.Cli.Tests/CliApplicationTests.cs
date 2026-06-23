using Runiq.Cli;

namespace Runiq.Cli.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public void Help_PrintsUsage()
    {
        using var output = new StringWriter();
        var app = CreateApp(output);

        var exitCode = app.Run(["--help"]);

        var text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", text);
        Assert.Contains("runiq init [project-name]", text);
        Assert.Contains("Project name is optional.", text);
        Assert.Contains("Commands:", text);
        Assert.Contains("init", text);
        Assert.DoesNotContain("doctor", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Version_PrintsVersion()
    {
        using var output = new StringWriter();
        var app = CreateApp(output, version: "1.0.0");

        var exitCode = app.Run(["--version"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("Runiq CLI 1.0.0", output.ToString().Trim());
    }

    [Fact]
    public void MissingCommand_ShowsFriendlyMessage()
    {
        using var output = new StringWriter();
        var app = CreateApp(output);

        var exitCode = app.Run([]);

        var text = output.ToString();
        Assert.Equal(1, exitCode);
        Assert.Contains("No command specified.", text);
        Assert.Contains("runiq --help", text);
    }

    [Fact]
    public void UnknownCommand_ShowsFriendlyMessage()
    {
        using var output = new StringWriter();
        var app = CreateApp(output);

        var exitCode = app.Run(["wat"]);

        var text = output.ToString();
        Assert.Equal(1, exitCode);
        Assert.Contains("Unknown command: wat", text);
        Assert.Contains("runiq --help", text);
    }

    [Fact]
    public void Init_WithoutProjectName_EntersInteractiveMode()
    {
        using var output = new StringWriter();
        var initWasCalled = false;
        string? projectName = "not-null";
        var app = new CliApplication(
            (name, _) =>
            {
                initWasCalled = true;
                projectName = name;
                return 0;
            },
            output);

        var exitCode = app.Run(["init"]);

        Assert.Equal(0, exitCode);
        Assert.True(initWasCalled);
        Assert.Null(projectName);
    }

    [Fact]
    public void Init_WithProjectName_ExecutesInitCommandWithoutPromptingForName()
    {
        using var output = new StringWriter();
        string? projectName = string.Empty;
        var debug = true;
        var app = new CliApplication(
            (name, isDebug) =>
            {
                projectName = name;
                debug = isDebug;
                return 0;
            },
            output);

        var exitCode = app.Run(["init", "MyRuniqApp"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("MyRuniqApp", projectName);
        Assert.False(debug);
    }

    [Fact]
    public void Init_EmptyProjectName_ShowsFriendlyUsage()
    {
        using var output = new StringWriter();
        var app = CreateApp(output);

        var exitCode = app.Run(["init", ""]);

        var text = output.ToString();
        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid project name:", text);
        Assert.Contains("Project name cannot be empty.", text);
        Assert.Contains("Usage: runiq init [project-name]", text);
    }

    [Fact]
    public void Init_InvalidProjectName_ShowsFriendlyUsage()
    {
        using var output = new StringWriter();
        var app = CreateApp(output);

        var exitCode = app.Run(["init", "123bad"]);

        var text = output.ToString();
        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid project name:", text);
        Assert.Contains("Project name must start with a letter or underscore", text);
        Assert.Contains("Usage: runiq init [project-name]", text);
    }

    private static CliApplication CreateApp(
        TextWriter output,
        string version = "9.9.9")
    {
        return new CliApplication(
            (_, _) => 0,
            output,
            () => version);
    }
}
