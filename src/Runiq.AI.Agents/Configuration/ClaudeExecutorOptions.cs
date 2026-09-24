namespace Runiq.AI.Agents.Configuration;

/// <summary>Configures the agent-selected local Claude CLI executor.</summary>
public sealed class ClaudeExecutorOptions
{
    /// <summary>Gets or sets an absolute native executable path; null discovers claude on PATH.</summary>
    /// <remarks>Windows shell shims (.cmd and .ps1) are not executed. Use the native claude.exe.</remarks>
    public string? ExecutablePath { get; set; }

    /// <summary>Gets or sets the absolute repository directory used for new and resumed executions.</summary>
    /// <remarks>An empty value uses the host content root, or the current directory when no host environment is registered.</remarks>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the maximum duration of one CLI invocation, including output consumption.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets or sets the maximum characters in a JSONL record before execution is stopped.</summary>
    public int MaxEventCharacters { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum stdout characters per invocation, bounding result aggregation.</summary>
    public int MaxOutputCharacters { get; set; } = 8_388_608;
}

