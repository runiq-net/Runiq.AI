using Spectre.Console;

namespace Runiq.AI.Cli.Interactive;

public static class ConsoleBanner
{
    public static void Show()
    {
        AnsiConsole.MarkupLine("[bold]Runiq Create[/]");
        AnsiConsole.WriteLine();
    }
}

