namespace Runiq.AI.CLI.Agents.Data;

/// <summary>
/// Represents the metrics returned for a service window.
/// </summary>
public sealed class ServiceMetricsResult
{
    /// <summary>Gets or sets whether the requested service was found.</summary>
    public bool Found { get; set; }

    /// <summary>Gets or sets the normalized service name.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Gets or sets the inclusive metrics window start.</summary>
    public DateTimeOffset? WindowStartUtc { get; set; }

    /// <summary>Gets or sets the inclusive metrics window end.</summary>
    public DateTimeOffset? WindowEndUtc { get; set; }

    /// <summary>Gets the metric points selected for the requested window.</summary>
    public List<MetricPoint> Points { get; } = [];

    /// <summary>Gets the anomalies selected for the requested window.</summary>
    public List<MetricAnomaly> Anomalies { get; } = [];

    /// <summary>Gets the source labels used to produce this result.</summary>
    public List<string> Sources { get; } = [];

    /// <summary>Gets or sets a controlled message for missing or invalid requests.</summary>
    public string? Message { get; set; }
}

/// <summary>
/// Represents a point-in-time service metric sample.
/// </summary>
public sealed class MetricPoint
{
    /// <summary>Gets or sets the timestamp of the metric point.</summary>
    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>Gets or sets the error rate percentage.</summary>
    public double ErrorRatePercent { get; set; }

    /// <summary>Gets or sets the p95 latency in milliseconds.</summary>
    public int P95LatencyMs { get; set; }

    /// <summary>Gets or sets the request throughput per minute.</summary>
    public int RequestsPerMinute { get; set; }

    /// <summary>Gets or sets dependency timeout count in the sample interval.</summary>
    public int DependencyTimeouts { get; set; }
}

/// <summary>
/// Represents a deterministic anomaly label from the sample metrics.
/// </summary>
public sealed class MetricAnomaly
{
    /// <summary>Gets or sets the anomaly code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets a human-readable anomaly summary.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp where the anomaly starts.</summary>
    public DateTimeOffset StartsAtUtc { get; set; }

    /// <summary>Gets or sets the baseline value used by the sample.</summary>
    public string Baseline { get; set; } = string.Empty;

    /// <summary>Gets or sets the observed value in the incident window.</summary>
    public string Observed { get; set; } = string.Empty;
}

/// <summary>
/// Represents the deployment records returned for a service.
/// </summary>
public sealed class RecentDeploymentsResult
{
    /// <summary>Gets or sets whether the requested service was found.</summary>
    public bool Found { get; set; }

    /// <summary>Gets or sets the normalized service name.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Gets the deployment records selected for the requested service.</summary>
    public List<DeploymentRecord> Deployments { get; } = [];

    /// <summary>Gets the source labels used to produce this result.</summary>
    public List<string> Sources { get; } = [];

    /// <summary>Gets or sets a controlled message for missing or invalid requests.</summary>
    public string? Message { get; set; }
}

/// <summary>
/// Represents one deployment event from the deterministic sample data.
/// </summary>
public sealed class DeploymentRecord
{
    /// <summary>Gets or sets the deployment timestamp.</summary>
    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>Gets or sets the deployed version.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Gets or sets the sample commit identifier.</summary>
    public string Commit { get; set; } = string.Empty;

    /// <summary>Gets or sets the deployment owner.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>Gets or sets the deployment change summary.</summary>
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Represents a runbook lookup result.
/// </summary>
public sealed class RunbookResult
{
    /// <summary>Gets or sets whether a matching runbook was found.</summary>
    public bool Found { get; set; }

    /// <summary>Gets or sets the normalized service name.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Gets or sets the normalized incident type.</summary>
    public string? IncidentType { get; set; }

    /// <summary>Gets or sets the runbook title.</summary>
    public string? Title { get; set; }

    /// <summary>Gets the first checks recommended by the runbook.</summary>
    public List<string> FirstChecks { get; } = [];

    /// <summary>Gets notes about human escalation for this sample runbook.</summary>
    public List<string> EscalationNotes { get; } = [];

    /// <summary>Gets the source labels used to produce this result.</summary>
    public List<string> Sources { get; } = [];

    /// <summary>Gets or sets a controlled message for missing or invalid requests.</summary>
    public string? Message { get; set; }
}
