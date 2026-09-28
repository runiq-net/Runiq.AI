using System.Text.Json;

namespace Runiq.AI.CLI.Agents.Data;

/// <summary>
/// Loads deterministic incident data from local JSON fixture files.
/// </summary>
public sealed class JsonIncidentDataSource : IIncidentDataSource
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _fixturesPath;

    /// <summary>
    /// Initializes the data source with the fixture directory path.
    /// </summary>
    /// <param name="fixturesPath">The directory that contains the JSON fixture files.</param>
    public JsonIncidentDataSource(string fixturesPath)
    {
        if (string.IsNullOrWhiteSpace(fixturesPath))
            throw new ArgumentException("Fixture path cannot be empty.", nameof(fixturesPath));

        _fixturesPath = fixturesPath;
    }

    /// <inheritdoc />
    public async Task<ServiceMetricsResult> GetServiceMetricsAsync(
        string? serviceName,
        DateTimeOffset? startUtc,
        DateTimeOffset? endUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedService = Normalize(serviceName);
        if (normalizedService is null)
        {
            return new ServiceMetricsResult
            {
                Found = false,
                Message = "Enter a valid service name to inspect metrics."
            };
        }

        var fixture = await ReadFixtureAsync<ServiceMetricsFixture>("service-metrics.json", cancellationToken);
        var service = fixture.Services.FirstOrDefault(candidate =>
            string.Equals(candidate.ServiceName, normalizedService, StringComparison.OrdinalIgnoreCase));
        if (service is null)
        {
            return new ServiceMetricsResult
            {
                Found = false,
                ServiceName = normalizedService,
                Message = $"Service '{normalizedService}' was not found in the demo metrics fixture."
            };
        }

        var lower = startUtc ?? DateTimeOffset.MinValue;
        var upper = endUtc ?? DateTimeOffset.MaxValue;
        if (lower > upper)
        {
            return new ServiceMetricsResult
            {
                Found = false,
                ServiceName = normalizedService,
                WindowStartUtc = startUtc,
                WindowEndUtc = endUtc,
                Message = "StartUtc must be earlier than or equal to EndUtc for the demo metrics window."
            };
        }

        var points = service.Points
            .Where(point => point.TimestampUtc >= lower && point.TimestampUtc <= upper)
            .OrderBy(point => point.TimestampUtc)
            .ToList();
        var anomalies = service.Anomalies
            .Where(anomaly => anomaly.StartsAtUtc >= lower && anomaly.StartsAtUtc <= upper)
            .OrderBy(anomaly => anomaly.StartsAtUtc)
            .ToList();

        var result = new ServiceMetricsResult
        {
            Found = true,
            ServiceName = service.ServiceName,
            WindowStartUtc = points.FirstOrDefault()?.TimestampUtc ?? startUtc,
            WindowEndUtc = points.LastOrDefault()?.TimestampUtc ?? endUtc,
            Message = points.Count == 0
                ? "No metric points matched the requested demo window."
                : $"Returned {points.Count} metric points from the demo fixture."
        };
        result.Points.AddRange(points);
        result.Anomalies.AddRange(anomalies);
        result.Sources.AddRange(service.Sources);
        return result;
    }

    /// <inheritdoc />
    public async Task<RecentDeploymentsResult> GetRecentDeploymentsAsync(
        string? serviceName,
        DateTimeOffset? sinceUtc,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedService = Normalize(serviceName);
        if (normalizedService is null)
        {
            return new RecentDeploymentsResult
            {
                Found = false,
                Message = "Enter a valid service name to inspect deployments."
            };
        }

        var fixture = await ReadFixtureAsync<DeploymentsFixture>("deployments.json", cancellationToken);
        var service = fixture.Services.FirstOrDefault(candidate =>
            string.Equals(candidate.ServiceName, normalizedService, StringComparison.OrdinalIgnoreCase));
        if (service is null)
        {
            return new RecentDeploymentsResult
            {
                Found = false,
                ServiceName = normalizedService,
                Message = $"Service '{normalizedService}' was not found in the demo deployments fixture."
            };
        }

        var lower = sinceUtc ?? DateTimeOffset.MinValue;
        var take = Math.Clamp(limit.GetValueOrDefault(5), 1, 20);
        var deployments = service.Deployments
            .Where(deployment => deployment.TimestampUtc >= lower)
            .OrderByDescending(deployment => deployment.TimestampUtc)
            .Take(take)
            .ToList();

        var result = new RecentDeploymentsResult
        {
            Found = true,
            ServiceName = service.ServiceName,
            Message = deployments.Count == 0
                ? "No deployment records matched the requested demo window."
                : $"Returned {deployments.Count} deployment records from the demo fixture."
        };
        result.Deployments.AddRange(deployments);
        result.Sources.AddRange(service.Sources);
        return result;
    }

    /// <inheritdoc />
    public async Task<RunbookResult> GetRunbookAsync(
        string? serviceName,
        string? incidentType,
        CancellationToken cancellationToken = default)
    {
        var normalizedService = Normalize(serviceName);
        var normalizedIncidentType = Normalize(incidentType);
        if (normalizedService is null || normalizedIncidentType is null)
        {
            return new RunbookResult
            {
                Found = false,
                ServiceName = normalizedService,
                IncidentType = normalizedIncidentType,
                Message = "Enter a valid service name and incident type to inspect the runbook."
            };
        }

        var fixture = await ReadFixtureAsync<RunbooksFixture>("runbooks.json", cancellationToken);
        var runbook = fixture.Runbooks.FirstOrDefault(candidate =>
            string.Equals(candidate.ServiceName, normalizedService, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.IncidentType, normalizedIncidentType, StringComparison.OrdinalIgnoreCase));
        if (runbook is null)
        {
            return new RunbookResult
            {
                Found = false,
                ServiceName = normalizedService,
                IncidentType = normalizedIncidentType,
                Message = $"No demo runbook matched service '{normalizedService}' and incident type '{normalizedIncidentType}'."
            };
        }

        var result = new RunbookResult
        {
            Found = true,
            ServiceName = runbook.ServiceName,
            IncidentType = runbook.IncidentType,
            Title = runbook.Title,
            Message = "Returned the matching demo runbook."
        };
        result.FirstChecks.AddRange(runbook.FirstChecks);
        result.EscalationNotes.AddRange(runbook.EscalationNotes);
        result.Sources.AddRange(runbook.Sources);
        return result;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<TFixture> ReadFixtureAsync<TFixture>(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_fixturesPath, fileName);
        await using var stream = File.OpenRead(path);
        var fixture = await JsonSerializer.DeserializeAsync<TFixture>(
            stream,
            SerializerOptions,
            cancellationToken);
        return fixture ?? throw new InvalidOperationException($"Fixture '{fileName}' was empty.");
    }

    private sealed class ServiceMetricsFixture
    {
        public List<ServiceMetricsRecord> Services { get; set; } = [];
    }

    private sealed class ServiceMetricsRecord
    {
        public string ServiceName { get; set; } = string.Empty;

        public List<MetricPoint> Points { get; set; } = [];

        public List<MetricAnomaly> Anomalies { get; set; } = [];

        public List<string> Sources { get; set; } = [];
    }

    private sealed class DeploymentsFixture
    {
        public List<ServiceDeploymentsRecord> Services { get; set; } = [];
    }

    private sealed class ServiceDeploymentsRecord
    {
        public string ServiceName { get; set; } = string.Empty;

        public List<DeploymentRecord> Deployments { get; set; } = [];

        public List<string> Sources { get; set; } = [];
    }

    private sealed class RunbooksFixture
    {
        public List<RunbookRecord> Runbooks { get; set; } = [];
    }

    private sealed class RunbookRecord
    {
        public string ServiceName { get; set; } = string.Empty;

        public string IncidentType { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public List<string> FirstChecks { get; set; } = [];

        public List<string> EscalationNotes { get; set; } = [];

        public List<string> Sources { get; set; } = [];
    }
}
