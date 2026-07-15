using Runiq.AI.Cli.Execution;
using Runiq.AI.Cli.Infrastructure;
using Runiq.AI.Cli.Models;
using Runiq.AI.Cli.Planning;

namespace Runiq.AI.Cli.Generation;

public sealed class ProjectGenerator
{
    private readonly ProjectPlanner _planner;
    private readonly PlanExecutor _executor;
    private readonly DotNetProjectGenerator _dotNetProjectGenerator;
    private readonly SolutionGenerator _solutionGenerator;
    private readonly RuniqIntegrationGenerator _runiqIntegrationGenerator;
    private readonly ArtifactGenerator _artifactGenerator;

    public ProjectGenerator()
    {
        var fileSystem = new PhysicalFileSystem();
        var processRunner = new ProcessRunner();

        _planner = new ProjectPlanner();

        _executor = new PlanExecutor(
            fileSystem);

        _dotNetProjectGenerator = new DotNetProjectGenerator(
            processRunner);

        _solutionGenerator = new SolutionGenerator(
            processRunner);

        _runiqIntegrationGenerator = new RuniqIntegrationGenerator(
            fileSystem,
            processRunner);

        _artifactGenerator = new ArtifactGenerator(
            fileSystem);
    }

    public GenerationResult Generate(
        ProjectDefinition definition,
        IGenerationProgressReporter? progress = null)
    {
        var plan = _planner.CreatePlan(definition);

        progress?.Start(GenerationStep.ProjectStructure);
        _executor.Execute(plan);
        progress?.Complete(GenerationStep.ProjectStructure);

        progress?.Start(GenerationStep.AspNetCoreProject);
        var apiProjectPath = _dotNetProjectGenerator.Generate(definition);
        progress?.Complete(GenerationStep.AspNetCoreProject);

        progress?.Start(GenerationStep.Solution);
        _solutionGenerator.Generate(
            definition,
            apiProjectPath);
        progress?.Complete(GenerationStep.Solution);

        progress?.Start(GenerationStep.RuniqPackages);
        _runiqIntegrationGenerator.Generate(
            definition,
            apiProjectPath);
        progress?.Complete(GenerationStep.RuniqPackages);

        progress?.Start(GenerationStep.RuniqArtifacts);
        _artifactGenerator.Generate(definition);
        progress?.Complete(GenerationStep.RuniqArtifacts);

        return new GenerationResult
        {
            Success = true,
            Message =
                $"Project '{definition.Name}' generated successfully."
        };
    }
}

