using Runiq.AI.Core.Configuration;
using Runiq.AI.Core.Models;

namespace Runiq.AI.Agents.Configuration;

/// <summary>Identifies the execution preference of an agent.</summary>
public enum AgentExecutorKind
{
    /// <summary>Executes through the model provider pipeline.</summary>
    Model,
    /// <summary>Requests Codex execution; no built-in implementation is supplied.</summary>
    Codex,
    /// <summary>Requests Claude execution; no built-in implementation is supplied.</summary>
    Claude
}

/// <summary>Describes the single executor selected through an agent's Use methods.</summary>
public sealed class AgentExecutorConfiguration
{
    internal AgentExecutorConfiguration(AgentExecutorKind kind, AgentModelConfiguration? model = null)
    {
        Kind = kind;
        Model = model;
    }

    /// <summary>Gets the selected executor kind.</summary>
    public AgentExecutorKind Kind { get; }

    /// <summary>Gets model-specific settings, or null for Codex and Claude.</summary>
    public AgentModelConfiguration? Model { get; }
}

/// <summary>Contains the validated settings used only by a model executor.</summary>
public sealed class AgentModelConfiguration
{
    internal AgentModelConfiguration(string model, string? apiKey, ProviderOptions? provider,
        string reasoningEffort, string verbosity)
    {
        Model = ValidateRequired(model, nameof(model));
        ModelReference = ModelReference.Parse(Model);
        ApiKey = apiKey;
        Provider = provider;
        ReasoningEffort = ValidateReasoningEffort(reasoningEffort);
        Verbosity = ValidateVerbosity(verbosity);
    }

    /// <summary>Gets the trimmed provider/model identifier.</summary>
    public string Model { get; }
    /// <summary>Gets the parsed provider and model reference.</summary>
    public ModelReference ModelReference { get; }
    /// <summary>Gets the optional provider API key.</summary>
    public string? ApiKey { get; }
    /// <summary>Gets the optional provider runtime settings.</summary>
    public ProviderOptions? Provider { get; }
    /// <summary>Gets the normalized reasoning effort.</summary>
    public string ReasoningEffort { get; }
    /// <summary>Gets the normalized response verbosity.</summary>
    public string Verbosity { get; }

    private static string ValidateRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
        return value.Trim();
    }

    private static string ValidateReasoningEffort(string value)
    {
        var normalized = ValidateRequired(value, nameof(ReasoningEffort)).ToLowerInvariant();

        return normalized switch
        {
            "minimal" => normalized,
            "low" => normalized,
            "medium" => normalized,
            "high" => normalized,
            _ => throw new ArgumentException(
                "Reasoning effort must be one of: minimal, low, medium, high.",
                nameof(ReasoningEffort))
        };
    }

    private static string ValidateVerbosity(string value)
    {
        var normalized = ValidateRequired(value, nameof(Verbosity)).ToLowerInvariant();

        return normalized switch
        {
            "low" => normalized,
            "medium" => normalized,
            "high" => normalized,
            _ => throw new ArgumentException(
                "Verbosity must be one of: low, medium, high.",
                nameof(Verbosity))
        };
    }
}
