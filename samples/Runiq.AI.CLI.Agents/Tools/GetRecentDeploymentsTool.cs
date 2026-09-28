using Runiq.AI.Agents.Tools;
using Runiq.AI.CLI.Agents.Data;

namespace Runiq.AI.CLI.Agents.Tools;

/// <summary>
/// Returns deterministic recent deployment records for the incident triage sample.
/// </summary>
[RuniqTool(
    name: "get_recent_deployments",
    description: "Returns demo deployment records for a service after a requested timestamp.")]
public sealed class GetRecentDeploymentsTool : IRuniqTool<GetRecentDeploymentsInput, RecentDeploymentsResult>
{
    private readonly IIncidentDataSource _dataSource;

    /// <summary>
    /// Initializes the tool with the shared incident data source.
    /// </summary>
    /// <param name="dataSource">The deterministic incident data source.</param>
    public GetRecentDeploymentsTool(IIncidentDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public Task<RecentDeploymentsResult> ExecuteAsync(
        GetRecentDeploymentsInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return _dataSource.GetRecentDeploymentsAsync(
            input.ServiceName,
            input.SinceUtc,
            input.Limit,
            cancellationToken);
    }
}

/// <summary>
/// Describes the deployment lookup requested by the agent.
/// </summary>
public sealed class GetRecentDeploymentsInput
{
    /// <summary>Gets or sets the service name to inspect.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Gets or sets the inclusive lower bound for deployment timestamps.</summary>
    public DateTimeOffset? SinceUtc { get; set; }

    /// <summary>Gets or sets the maximum deployment count to return.</summary>
    public int? Limit { get; set; }
}
