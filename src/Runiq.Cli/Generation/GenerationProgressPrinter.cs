using Spectre.Console;

namespace Runiq.Cli.Generation;

public sealed class GenerationProgressPrinter : IGenerationProgressReporter
{
    public void Start(GenerationStep step)
    {
        AnsiConsole.MarkupLine($"[grey]◇ {GetStartMessage(step)}[/]");
    }

    public void Complete(GenerationStep step)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {GetCompleteMessage(step)}");
        AnsiConsole.WriteLine();
    }

    private static string GetStartMessage(GenerationStep step)
    {
        return step switch
        {
            GenerationStep.ProjectStructure => "Creating project structure",
            GenerationStep.AspNetCoreProject => "Creating ASP.NET Core project",
            GenerationStep.Solution => "Creating solution",
            GenerationStep.RuniqPackages => "Adding Runiq packages",
            GenerationStep.RuniqArtifacts => "Generating Runiq artifacts",
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null)
        };
    }

    private static string GetCompleteMessage(GenerationStep step)
    {
        return step switch
        {
            GenerationStep.ProjectStructure => "Project structure created",
            GenerationStep.AspNetCoreProject => "ASP.NET Core project created",
            GenerationStep.Solution => "Solution created",
            GenerationStep.RuniqPackages => "Runiq packages added",
            GenerationStep.RuniqArtifacts => "Runiq artifacts generated",
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null)
        };
    }
}
