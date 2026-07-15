namespace Runiq.AI.Cli.Planning;

public sealed class ProjectPlan
{
    public IReadOnlyList<DirectoryPlan> Directories { get; init; } =
        Array.Empty<DirectoryPlan>();

    public IReadOnlyList<FilePlan> Files { get; init; } =
        Array.Empty<FilePlan>();
}
