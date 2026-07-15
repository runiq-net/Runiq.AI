using Runiq.AI.Cli.Infrastructure;
using Runiq.AI.Cli.Models;

namespace Runiq.AI.Cli.Generation;

public sealed class SolutionGenerator
{
    private readonly IProcessRunner _processRunner;

    public SolutionGenerator(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public string Generate(
        ProjectDefinition definition,
        string apiProjectPath)
    {
        var rootPath = Path.GetFullPath(definition.Name);
        var solutionPath = Path.Combine(rootPath, $"{definition.Name}.sln");

        RunDotNet(
            [
                "new",
                "sln",
                "--name",
                definition.Name,
                "--output",
                rootPath,
                "--format",
                "sln"
            ],
            Directory.GetCurrentDirectory());

        RunDotNet(
            [
                "sln",
                solutionPath,
                "add",
                apiProjectPath
            ],
            rootPath);

        return solutionPath;
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

