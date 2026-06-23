using Runiq.Cli.Infrastructure;
using Runiq.Cli.Planning;

namespace Runiq.Cli.Execution;

public sealed class PlanExecutor
{
    private readonly IFileSystem _fileSystem;

    public PlanExecutor(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public void Execute(ProjectPlan plan)
    {
        foreach (var directory in plan.Directories)
        {
            _fileSystem.CreateDirectory(directory.Path);
        }

        foreach (var file in plan.Files)
        {
            _fileSystem.WriteAllText(
                file.Path,
                file.Content);
        }
    }
}