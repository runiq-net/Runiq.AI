namespace Runiq.Cli.Models;

public sealed class ProjectDefinition
{
    public string Name { get; init; } = string.Empty;

    public AiProvider Provider { get; init; }

    public bool EnableDashboard { get; init; }

    public bool EnableMcp { get; init; }

    public bool IncludeSampleCode { get; init; }

    public ApiKeySetupMode ApiKeySetupMode { get; init; }

    internal string? ApiKeyValue { get; init; }

    internal string? AzureOpenAiEndpoint { get; init; }
}
