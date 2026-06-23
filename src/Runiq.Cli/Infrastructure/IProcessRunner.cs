namespace Runiq.Cli.Infrastructure;

public interface IProcessRunner
{
    ProcessResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory);
}
