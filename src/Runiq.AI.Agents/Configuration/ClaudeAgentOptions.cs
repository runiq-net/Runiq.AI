namespace Runiq.AI.Agents.Configuration;

/// <summary>Configures one Claude agent independently of host process settings.</summary>
public sealed class ClaudeAgentOptions
{
    /// <summary>Gets or sets the required CLI model name or alias. The CLI validates availability.</summary>
    public string Model { get; set; } = null!;

    /// <summary>Gets or sets the requested reasoning effort, defaulting to High.</summary>
    public ClaudeReasoningEffort ReasoningEffort { get; set; } = ClaudeReasoningEffort.High;
}

/// <summary>Represents Claude reasoning levels; support depends on the CLI, model and account policy.</summary>
public enum ClaudeReasoningEffort
{
    /// <summary>Requests low reasoning effort.</summary>
    Low,
    /// <summary>Requests medium reasoning effort.</summary>
    Medium,
    /// <summary>Requests high reasoning effort.</summary>
    High,
    /// <summary>Requests extra-high reasoning effort where supported.</summary>
    XHigh,
    /// <summary>Requests maximum reasoning effort where supported.</summary>
    Max
}

/// <summary>Contains an immutable snapshot of one Claude agent's validated model settings.</summary>
public sealed class ClaudeAgentConfiguration
{
    internal ClaudeAgentConfiguration(ClaudeAgentOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Model);
        if (!Enum.IsDefined(options.ReasoningEffort))
            throw new ArgumentException("Unknown Claude reasoning effort.", nameof(options.ReasoningEffort));
        Model = options.Model.Trim();
        ReasoningEffort = options.ReasoningEffort;
    }

    /// <summary>Gets the explicit model name or alias passed on every turn, including resume.</summary>
    public string Model { get; }

    /// <summary>Gets the reasoning effort requested on every turn, subject to CLI support and policy.</summary>
    public ClaudeReasoningEffort ReasoningEffort { get; }
}
