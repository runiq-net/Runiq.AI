using System.Reflection;
using Runiq.Cli.Commands;

namespace Runiq.Cli;

public sealed class CliApplication
{
    private readonly Func<string?, bool, int> _executeInit;
    private readonly TextWriter _output;
    private readonly Func<string> _versionProvider;

    public CliApplication(
        Func<string?, bool, int> executeInit,
        TextWriter? output = null,
        Func<string>? versionProvider = null)
    {
        _executeInit = executeInit;
        _output = output ?? Console.Out;
        _versionProvider = versionProvider ?? GetVersion;
    }

    public int Run(string[] args)
    {
        if (args.Length == 0)
        {
            WriteMissingCommand();
            return 1;
        }

        var command = args[0];

        if (IsHelp(command))
        {
            WriteHelp();
            return 0;
        }

        if (command.Equals("--version", StringComparison.OrdinalIgnoreCase))
        {
            _output.WriteLine($"Runiq CLI {_versionProvider()}");
            return 0;
        }

        if (command.Equals(CommandNames.Init, StringComparison.OrdinalIgnoreCase))
        {
            return RunInit(args.Skip(1).ToArray());
        }

        _output.WriteLine($"Unknown command: {command}");
        _output.WriteLine("Run 'runiq --help' to see available commands.");

        return 1;
    }

    private int RunInit(string[] args)
    {
        var debug = false;
        string? projectName = null;

        foreach (var argument in args)
        {
            if (argument.Equals("--debug", StringComparison.OrdinalIgnoreCase))
            {
                debug = true;
                continue;
            }

            if (argument.StartsWith("-", StringComparison.Ordinal))
            {
                _output.WriteLine($"Unknown option for 'init': {argument}");
                _output.WriteLine("Usage: runiq init [project-name]");
                return 1;
            }

            if (projectName is not null)
            {
                _output.WriteLine("Too many arguments for 'init'.");
                _output.WriteLine("Usage: runiq init [project-name]");
                return 1;
            }

            projectName = argument;
        }

        if (projectName is not null
            && !ProjectNameValidator.TryValidate(projectName, out var error))
        {
            _output.WriteLine($"Invalid project name: {error}");
            _output.WriteLine("Usage: runiq init [project-name]");
            return 1;
        }

        return _executeInit(projectName, debug);
    }

    private void WriteMissingCommand()
    {
        _output.WriteLine("No command specified.");
        _output.WriteLine("Run 'runiq --help' to see available commands.");
    }

    private void WriteHelp()
    {
        _output.WriteLine("Runiq CLI");
        _output.WriteLine();
        _output.WriteLine("Usage:");
        _output.WriteLine("  runiq --help");
        _output.WriteLine("  runiq --version");
        _output.WriteLine("  runiq init [project-name]");
        _output.WriteLine();
        _output.WriteLine("Commands:");
        _output.WriteLine("  init    Create a new Runiq project. Project name is optional.");
    }

    private static bool IsHelp(string command)
    {
        return command.Equals("--help", StringComparison.OrdinalIgnoreCase)
            || command.Equals("-h", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetVersion()
    {
        var informationalVersion = typeof(CliApplication)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+')[0];
        }

        return typeof(CliApplication).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
