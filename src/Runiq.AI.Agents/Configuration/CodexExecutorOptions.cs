namespace Runiq.AI.Agents.Configuration;

/// <summary>Configures the explicitly enabled local Codex CLI executor.</summary>
public sealed class CodexExecutorOptions
{
    /// <summary>Gets or sets an absolute native executable path; null discovers codex on PATH.</summary>
    /// <remarks>Windows shell shims (.cmd and .ps1) are not executed. Use the native codex.exe.</remarks>
    public string? ExecutablePath { get; set; }

    /// <summary>Gets or sets the absolute repository directory used for new and resumed executions.</summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the maximum duration of one CLI invocation, including output consumption.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets or sets the CLI sandbox; read-only is the default and approval prompts are disabled.</summary>
    public CodexSandboxMode Sandbox { get; set; } = CodexSandboxMode.ReadOnly;

    /// <summary>Gets or sets whether execution outside a Git repository is explicitly permitted.</summary>
    public bool SkipGitRepositoryCheck { get; set; }

    /// <summary>Gets or sets the maximum characters in a JSONL record before execution is stopped.</summary>
    public int MaxEventCharacters { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum stdout characters per invocation, bounding result aggregation.</summary>
    public int MaxOutputCharacters { get; set; } = 8_388_608;
}

/// <summary>Defines the supported non-interactive Codex filesystem policies.</summary>
public enum CodexSandboxMode
{
    /// <summary>Allows reading the workspace without granting write access.</summary>
    ReadOnly,
    /// <summary>Allows changes in the configured workspace under the CLI sandbox.</summary>
    WorkspaceWrite
}
