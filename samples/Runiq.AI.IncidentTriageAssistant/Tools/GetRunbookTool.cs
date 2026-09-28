using Runiq.AI.Agents.Tools;
using Runiq.AI.IncidentTriageAssistant.Data;

namespace Runiq.AI.IncidentTriageAssistant.Tools;

/// <summary>
/// Returns deterministic runbook guidance for the incident triage sample.
/// </summary>
[RuniqTool(
    name: "get_runbook",
    description: "Returns demo runbook guidance for a service and incident type.")]
public sealed class GetRunbookTool : IRuniqTool<GetRunbookInput, RunbookResult>
{
    private readonly IIncidentDataSource _dataSource;

    /// <summary>
    /// Initializes the tool with the shared incident data source.
    /// </summary>
    /// <param name="dataSource">The deterministic incident data source.</param>
    public GetRunbookTool(IIncidentDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public Task<RunbookResult> ExecuteAsync(
        GetRunbookInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return _dataSource.GetRunbookAsync(
            input.ServiceName,
            input.IncidentType,
            cancellationToken);
    }
}

/// <summary>
/// Describes the runbook lookup requested by the agent.
/// </summary>
public sealed class GetRunbookInput
{
    /// <summary>Gets or sets the service name to inspect.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Gets or sets the incident type to inspect.</summary>
    public string? IncidentType { get; set; }
}
