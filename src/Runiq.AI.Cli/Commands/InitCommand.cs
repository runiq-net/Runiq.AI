using System.Text.Json;
using System.Text.Json.Serialization;
using Runiq.AI.Cli.Generation;
using Runiq.AI.Cli.Infrastructure;
using Runiq.AI.Cli.Interactive;
using Spectre.Console;

namespace Runiq.AI.Cli.Commands;

public sealed class InitCommand
{
    private readonly ProjectWizard _wizard;
    private readonly ProjectGenerator _generator;
    private readonly RunInstructionsPrinter _runInstructionsPrinter;
    private readonly GenerationProgressPrinter _generationProgressPrinter;

    public InitCommand()
    {
        _wizard = new ProjectWizard();
        _generator = new ProjectGenerator();
        _runInstructionsPrinter = new RunInstructionsPrinter(
            new LaunchSettingsReader(
                new PhysicalFileSystem()));
        _generationProgressPrinter = new GenerationProgressPrinter();
    }

    public int Execute(string? projectName, bool debug)
    {
        ConsoleBanner.Show();
        projectName ??= _wizard.AskProjectName();

        if (!ProjectNameValidator.TryValidate(projectName, out var error))
        {
            AnsiConsole.MarkupLine($"[red]Invalid project name:[/] {error}");
            return 1;
        }

        if (Directory.Exists(projectName)
            && Directory.EnumerateFileSystemEntries(projectName).Any())
        {
            AnsiConsole.MarkupLine($"[red]Target folder '{projectName}' already exists and is not empty.[/]");
            AnsiConsole.MarkupLine("Choose a new project name or an empty folder.");
            return 1;
        }

        var project = _wizard.Run(projectName);

        if (debug)
        {
            PrintDebugDefinition(project);
        }

        var result = _generator.Generate(
            project,
            _generationProgressPrinter);

        AnsiConsole.WriteLine();

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]{result.Message}[/]");
            _runInstructionsPrinter.Print(project);
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]{result.Message}[/]");
            return 1;
        }

        return 0;
    }

    private static void PrintDebugDefinition(object project)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel(JsonSerializer.Serialize(project, jsonOptions))
                .Header("Project Definition")
                .Border(BoxBorder.Rounded));
        AnsiConsole.WriteLine();
    }
}

