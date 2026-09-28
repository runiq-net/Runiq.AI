using Runiq.AI.CLI.Agents.Data;
using Runiq.AI.CLI.Agents.Tools;

namespace Runiq.AI.CLI.Agents.Tests;

public sealed class IncidentToolsTests
{
    [Fact]
    // Verifies the service metrics tool returns the expected deterministic anomaly output.
    public async Task GetServiceMetricsTool_OrderApiWindow_ReturnsExpectedOutput()
    {
        var tool = new GetServiceMetricsTool(new JsonIncidentDataSource(IncidentTriageTestPaths.FixturesPath));

        var result = await tool.ExecuteAsync(new GetServiceMetricsInput
        {
            ServiceName = " order-api ",
            StartUtc = DateTimeOffset.Parse("2026-09-28T10:00:00Z"),
            EndUtc = DateTimeOffset.Parse("2026-09-28T10:30:00Z")
        });

        Assert.True(result.Found);
        Assert.Equal(4, result.Points.Count);
        Assert.Contains(result.Anomalies, anomaly => anomaly.Code == "dependency_timeouts_increased");
    }

    [Fact]
    // Verifies the deployments tool applies limit and timestamp filtering to the deterministic deployment fixture.
    public async Task GetRecentDeploymentsTool_WithLimit_ReturnsExpectedOutput()
    {
        var tool = new GetRecentDeploymentsTool(new JsonIncidentDataSource(IncidentTriageTestPaths.FixturesPath));

        var result = await tool.ExecuteAsync(new GetRecentDeploymentsInput
        {
            ServiceName = "order-api",
            SinceUtc = DateTimeOffset.Parse("2026-09-27T00:00:00Z"),
            Limit = 2
        });

        Assert.True(result.Found);
        Assert.Equal(2, result.Deployments.Count);
        Assert.Equal("2026.09.28.3", result.Deployments[0].Version);
        Assert.Equal("2026.09.28.2", result.Deployments[1].Version);
    }

    [Fact]
    // Verifies the runbook tool returns first checks and escalation notes for the supported incident category.
    public async Task GetRunbookTool_KnownIncidentType_ReturnsExpectedOutput()
    {
        var tool = new GetRunbookTool(new JsonIncidentDataSource(IncidentTriageTestPaths.FixturesPath));

        var result = await tool.ExecuteAsync(new GetRunbookInput
        {
            ServiceName = "order-api",
            IncidentType = "elevated-error-rate"
        });

        Assert.True(result.Found);
        Assert.Equal("Order API elevated error rate first response", result.Title);
        Assert.Contains(result.FirstChecks, check => check.Contains("deployment timestamp", StringComparison.Ordinal));
        Assert.Contains(result.EscalationNotes, note => note.Contains("does not authorize automatic rollback", StringComparison.Ordinal));
    }

    [Fact]
    // Verifies invalid tool input produces a controlled not-found result instead of throwing from the tool.
    public async Task GetRunbookTool_InvalidInput_ReturnsControlledResult()
    {
        var tool = new GetRunbookTool(new JsonIncidentDataSource(IncidentTriageTestPaths.FixturesPath));

        var result = await tool.ExecuteAsync(new GetRunbookInput
        {
            ServiceName = " ",
            IncidentType = " "
        });

        Assert.False(result.Found);
        Assert.Contains("Enter a valid service name", result.Message, StringComparison.Ordinal);
    }
}
