using Runiq.Teams.Execution.Planning;
using Runiq.Teams.Models.Teams;

namespace Runiq.Teams.Tests.Execution.Planning;

/// <summary>
/// Sıralı team execution planner davranışlarını doğrular.
/// </summary>
public sealed class SequentialTeamExecutionPlannerTests
{
    [Fact]
    public async Task CreatePlanAsync_ShouldCreatePlanWithAllMembersInDeclaredOrder()
    {
        var team = new AgentTeam(
                id: "travel-team",
                name: "Travel Team",
                instructions: "Create travel plans.")
            .AddMember("weather-agent", "Weather Analyst")
            .AddMember("planner-agent", "Travel Planner");

        var planner = new SequentialTeamExecutionPlanner();

        var plan = await planner.CreatePlanAsync(team, "Plan a trip.");

        Assert.Equal("planner-agent", plan.FinalAgentId);
        Assert.Equal("Sequential team plan generated from declared member order.", plan.PlanningSummary);
        Assert.Collection(
            plan.Steps,
            first =>
            {
                Assert.Equal("weather-agent", first.AgentId);
                Assert.Equal(0, first.Order);
                Assert.False(first.IsFinalMember);
                Assert.False(string.IsNullOrWhiteSpace(first.Reason));
            },
            second =>
            {
                Assert.Equal("planner-agent", second.AgentId);
                Assert.Equal(1, second.Order);
                Assert.True(second.IsFinalMember);
                Assert.False(string.IsNullOrWhiteSpace(second.Reason));
            });
    }
}
