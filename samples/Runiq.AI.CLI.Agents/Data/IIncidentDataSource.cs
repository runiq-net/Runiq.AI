namespace Runiq.AI.CLI.Agents.Data;

/// <summary>
/// Provides deterministic incident data for the local sample tools.
/// </summary>
public interface IIncidentDataSource
{
    /// <summary>
    /// Gets service metrics for a service and fixed time window.
    /// </summary>
    /// <param name="serviceName">The service name requested by the agent.</param>
    /// <param name="startUtc">The inclusive start of the incident window.</param>
    /// <param name="endUtc">The inclusive end of the incident window.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching service metrics, or a controlled not-found result.</returns>
    Task<ServiceMetricsResult> GetServiceMetricsAsync(
        string? serviceName,
        DateTimeOffset? startUtc,
        DateTimeOffset? endUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent deployment records for a service.
    /// </summary>
    /// <param name="serviceName">The service name requested by the agent.</param>
    /// <param name="sinceUtc">The inclusive lower bound for deployment timestamps.</param>
    /// <param name="limit">The maximum number of deployment records to return.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching deployment records.</returns>
    Task<RecentDeploymentsResult> GetRecentDeploymentsAsync(
        string? serviceName,
        DateTimeOffset? sinceUtc,
        int? limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a runbook for a service and incident type.
    /// </summary>
    /// <param name="serviceName">The service name requested by the agent.</param>
    /// <param name="incidentType">The incident type requested by the agent.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching runbook, or a controlled not-found result.</returns>
    Task<RunbookResult> GetRunbookAsync(
        string? serviceName,
        string? incidentType,
        CancellationToken cancellationToken = default);
}
