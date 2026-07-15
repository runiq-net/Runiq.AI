namespace Runiq.AI.Cli.Infrastructure;

public interface IProcessRunner
{
    ProcessResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory);
}

