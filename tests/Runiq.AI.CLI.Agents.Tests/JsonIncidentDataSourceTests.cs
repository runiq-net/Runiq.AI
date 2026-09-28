using Runiq.AI.CLI.Agents.Data;

namespace Runiq.AI.CLI.Agents.Tests;

public sealed class JsonIncidentDataSourceTests
{
    [Fact]
    // Verifies the copied JSON fixtures are available to tests without requiring a CLI runtime.
    public void Fixtures_ShouldBeCopiedToTestOutput()
    {
        Assert.True(File.Exists(Path.Combine(IncidentTriageTestPaths.FixturesPath, "service-metrics.json")));
        Assert.True(File.Exists(Path.Combine(IncidentTriageTestPaths.FixturesPath, "deployments.json")));
        Assert.True(File.Exists(Path.Combine(IncidentTriageTestPaths.FixturesPath, "runbooks.json")));
    }

    [Fact]
    // Verifies the metrics fixture parses and returns deterministic anomalies for the incident window.
    public async Task GetServiceMetricsAsync_OrderApiIncidentWindow_ReturnsAnomalies()
    {
        var dataSource = new JsonIncidentDataSource(IncidentTriageTestPaths.FixturesPath);

        var result = await dataSource.GetServiceMetricsAsync(
            "order-api",
            DateTimeOffset.Parse("2026-09-28T09:30:00Z"),
            DateTimeOffset.Parse("2026-09-28T10:30:00Z"));

        Assert.True(result.Found);
        Assert.Equal("order-api", result.ServiceName);
        Assert.Equal(6, result.Points.Count);
        Assert.Contains(result.Anomalies, anomaly => anomaly.Code == "error_rate_above_baseline");
        Assert.Contains(result.Anomalies, anomaly => anomaly.Code == "p95_latency_above_baseline");
        Assert.Contains("demo fixture: service-metrics.json", result.Sources);
    }

    [Fact]
    // Verifies unknown services produce a controlled not-found metrics result.
    public async Task GetServiceMetricsAsync_UnknownService_ReturnsControlledResult()
    {
        var dataSource = new JsonIncidentDataSource(IncidentTriageTestPaths.FixturesPath);

        var result = await dataSource.GetServiceMetricsAsync(
            "catalog-api",
            DateTimeOffset.Parse("2026-09-28T09:30:00Z"),
            DateTimeOffset.Parse("2026-09-28T10:30:00Z"));

        Assert.False(result.Found);
        Assert.Empty(result.Points);
        Assert.Contains("was not found", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    // Verifies reversed metric windows produce a controlled invalid result instead of an empty successful lookup.
    public async Task GetServiceMetricsAsync_ReversedWindow_ReturnsControlledInvalidResult()
    {
        var dataSource = new JsonIncidentDataSource(IncidentTriageTestPaths.FixturesPath);

        var result = await dataSource.GetServiceMetricsAsync(
            "order-api",
            DateTimeOffset.Parse("2026-09-28T10:30:00Z"),
            DateTimeOffset.Parse("2026-09-28T09:30:00Z"));

        Assert.False(result.Found);
        Assert.Empty(result.Points);
        Assert.Contains("StartUtc must be earlier than or equal to EndUtc", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    // Verifies deployment filtering honors the lower timestamp bound and returns newest records first.
    public async Task GetRecentDeploymentsAsync_SinceTimestamp_ReturnsFilteredDeploymentsNewestFirst()
    {
        var dataSource = new JsonIncidentDataSource(IncidentTriageTestPaths.FixturesPath);

        var result = await dataSource.GetRecentDeploymentsAsync(
            "order-api",
            DateTimeOffset.Parse("2026-09-28T09:00:00Z"),
            limit: 5);

        var deployment = Assert.Single(result.Deployments);
        Assert.True(result.Found);
        Assert.Equal("2026.09.28.3", deployment.Version);
        Assert.Equal(DateTimeOffset.Parse("2026-09-28T10:04:00Z"), deployment.TimestampUtc);
        Assert.Contains("demo fixture: deployments.json", result.Sources);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(50, 3)]
    // Verifies deployment limit inputs are clamped to the supported demo range.
    public async Task GetRecentDeploymentsAsync_LimitBoundaries_AreClamped(int limit, int expectedCount)
    {
        var dataSource = new JsonIncidentDataSource(IncidentTriageTestPaths.FixturesPath);

        var result = await dataSource.GetRecentDeploymentsAsync(
            "order-api",
            DateTimeOffset.Parse("2026-09-27T00:00:00Z"),
            limit);

        Assert.True(result.Found);
        Assert.Equal(expectedCount, result.Deployments.Count);
    }

    [Fact]
    // Verifies unknown runbook categories produce a controlled not-found result.
    public async Task GetRunbookAsync_UnknownIncidentType_ReturnsControlledResult()
    {
        var dataSource = new JsonIncidentDataSource(IncidentTriageTestPaths.FixturesPath);

        var result = await dataSource.GetRunbookAsync("order-api", "database-failover");

        Assert.False(result.Found);
        Assert.Empty(result.FirstChecks);
        Assert.Contains("No demo runbook matched", result.Message, StringComparison.Ordinal);
    }
}
