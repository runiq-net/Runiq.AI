using System.Text.Json.Serialization;
using Runiq.AI.Agents.Configuration;

namespace Runiq.AI.Agents;

/// <summary>Preserves executor failure context separately from the short user-facing error message.</summary>
public sealed class AgentExecutionErrorDetails
{
    internal AgentExecutionErrorDetails(AgentExecutorKind executorKind, string requestedModel,
        int? exitCode, string? diagnosticDetail)
    {
        ExecutorKind = executorKind;
        RequestedModel = requestedModel;
        ExitCode = exitCode;
        DiagnosticDetail = diagnosticDetail;
    }

    /// <summary>Gets the executor that reported the failure.</summary>
    public AgentExecutorKind ExecutorKind { get; }
    /// <summary>Gets the model requested by the agent.</summary>
    public string RequestedModel { get; }
    /// <summary>Gets the underlying CLI exit code when the process was awaited.</summary>
    public int? ExitCode { get; }
    /// <summary>Gets bounded raw CLI diagnostics for local debugging; may contain sensitive data.</summary>
    /// <remarks>Excluded from JSON serialization and hosted error messages. Do not display to end users.</remarks>
    [JsonIgnore]
    public string? DiagnosticDetail { get; }
}
