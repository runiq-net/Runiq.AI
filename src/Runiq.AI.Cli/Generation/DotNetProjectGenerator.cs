using Runiq.AI.Cli.Infrastructure;
using Runiq.AI.Cli.Models;

namespace Runiq.AI.Cli.Generation;

public sealed class DotNetProjectGenerator
{
    private readonly IProcessRunner _processRunner;

    public DotNetProjectGenerator(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public string Generate(ProjectDefinition definition)
    {
        var projectName = $"{definition.Name}.Api";
        var projectPath = Path.GetFullPath(
            Path.Combine(definition.Name, "src", projectName));

        RunDotNet(
            [
                "new",
                "webapi",
                "--name",
                projectName,
                "--output",
                projectPath,
                "--framework",
                "net10.0"
            ],
            Directory.GetCurrentDirectory());

        return Path.Combine(projectPath, $"{projectName}.csproj");
    }

    private void RunDotNet(
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var result = _processRunner.Run(
            "dotnet",
            arguments,
            workingDirectory);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet {string.Join(' ', arguments)} failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardError}");
        }
    }
}

