using Runiq.Cli.Models;
using Spectre.Console;

namespace Runiq.Cli.Generation;

public sealed class RunInstructionsPrinter
{
    private readonly LaunchSettingsReader _launchSettingsReader;

    public RunInstructionsPrinter(LaunchSettingsReader launchSettingsReader)
    {
        _launchSettingsReader = launchSettingsReader;
    }

    public void Print(ProjectDefinition definition)
    {
        var applicationUrl = _launchSettingsReader.GetApplicationUrl(definition);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Next steps[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  cd {definition.Name}");
        AnsiConsole.MarkupLine($"  dotnet run --project src/{definition.Name}.Api");

        if (!definition.EnableDashboard && !definition.EnableMcp)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Open[/]");
        AnsiConsole.WriteLine();

        if (applicationUrl is null)
        {
            AnsiConsole.MarkupLine("  Run the API to see its listening URL.");
            return;
        }

        if (definition.EnableDashboard)
        {
            AnsiConsole.MarkupLine($"  Dashboard  {applicationUrl}/dashboard");
        }

        if (definition.EnableMcp)
        {
            AnsiConsole.MarkupLine($"  MCP        {applicationUrl}/mcp");
        }
    }
}
