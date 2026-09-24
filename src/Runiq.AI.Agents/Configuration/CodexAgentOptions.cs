namespace Runiq.AI.Agents.Configuration;

/// <summary>Configures the model used by one Codex agent, independently of host process settings.</summary>
public sealed class CodexAgentOptions
{
    /// <summary>Gets or sets the required model name. Availability is validated by the CLI, not Runiq.</summary>
    public string Model { get; set; } = null!;

    /// <summary>Gets or sets the reasoning effort, defaulting to High. The CLI validates model support.</summary>
    public CodexReasoningEffort ReasoningEffort { get; set; } = CodexReasoningEffort.High;

    /// <summary>Gets or sets the service tier. Default preserves the local CLI tier configuration.</summary>
    public CodexServiceTier ServiceTier { get; set; } = CodexServiceTier.Default;
}

/// <summary>Represents Codex reasoning levels without imposing model-specific compatibility rules.</summary>
public enum CodexReasoningEffort
{
    /// <summary>Requests no reasoning where supported.</summary>
    None,
    /// <summary>Requests minimal reasoning.</summary>
    Minimal,
    /// <summary>Requests low reasoning.</summary>
    Low,
    /// <summary>Requests medium reasoning.</summary>
    Medium,
    /// <summary>Requests high reasoning.</summary>
    High,
    /// <summary>Requests extra-high reasoning.</summary>
    XHigh,
    /// <summary>Requests maximum reasoning.</summary>
    Max,
    /// <summary>Requests ultra reasoning.</summary>
    Ultra
}

/// <summary>Represents the agent's service-tier preference for Codex CLI requests.</summary>
public enum CodexServiceTier
{
    /// <summary>Omits the override and preserves the local CLI configuration and defaults.</summary>
    Default,
    /// <summary>Requests the CLI fast tier, mapped by Codex to priority service where supported.</summary>
    Fast
}

/// <summary>Contains an immutable snapshot of the validated model settings for one Codex agent.</summary>
public sealed class CodexAgentConfiguration
{
    internal CodexAgentConfiguration(CodexAgentOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Model);
        if (!Enum.IsDefined(options.ReasoningEffort))
            throw new ArgumentException("Unknown Codex reasoning effort.", nameof(options.ReasoningEffort));
        if (!Enum.IsDefined(options.ServiceTier))
            throw new ArgumentException("Unknown Codex service tier.", nameof(options.ServiceTier));
        Model = options.Model.Trim();
        ReasoningEffort = options.ReasoningEffort;
        ServiceTier = options.ServiceTier;
    }

    /// <summary>Gets the explicit model name passed to Codex on every turn, including resume.</summary>
    public string Model { get; }
    /// <summary>Gets the reasoning effort passed to Codex on every turn.</summary>
    public CodexReasoningEffort ReasoningEffort { get; }
    /// <summary>Gets the service-tier preference for every turn.</summary>
    public CodexServiceTier ServiceTier { get; }
}
