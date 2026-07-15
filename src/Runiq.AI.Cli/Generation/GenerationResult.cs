namespace Runiq.AI.Cli.Generation;

public sealed class GenerationResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}
