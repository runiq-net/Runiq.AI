namespace Runiq.Cli.Generation;

public interface IGenerationProgressReporter
{
    void Start(GenerationStep step);

    void Complete(GenerationStep step);
}
