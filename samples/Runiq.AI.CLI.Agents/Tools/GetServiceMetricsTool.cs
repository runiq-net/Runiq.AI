using Runiq.AI.Agents.Tools;
using Runiq.AI.CLI.Agents.Data;

namespace Runiq.AI.CLI.Agents.Tools;

/// <summary>
/// Returns deterministic service metrics for the incident triage sample.
/// </summary>
[RuniqTool(
    name: "get_service_metrics",
    description: "Returns demo service metrics and detected anomalies for a fixed incident window.")]
public sealed class GetServiceMetricsTool : IRuniqTool<GetServiceMetricsInput, ServiceMetricsResult>
{
    private readonly IIncidentDataSource _dataSource;

    /// <summary>
    /// Initializes the tool with the shared incident data source.
    /// </summary>
    /// <param name="dataSource">The deterministic incident data source.</param>
    public GetServiceMetricsTool(IIncidentDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public Task<ServiceMetricsResult> ExecuteAsync(
        GetServiceMetricsInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return _dataSource.GetServiceMetricsAsync(
            input.ServiceName,
            input.StartUtc,
            input.EndUtc,
            cancellationToken);
    }
}

/// <summary>
/// Describes the service metrics lookup requested by the agent.
/// </summary>
public sealed class GetServiceMetricsInput
{
    /// <summary>Gets or sets the service name to inspect.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Gets or sets the inclusive start of the metrics window.</summary>
    public DateTimeOffset? StartUtc { get; set; }

    /// <summary>Gets or sets the inclusive end of the metrics window.</summary>
    public DateTimeOffset? EndUtc { get; set; }
}
