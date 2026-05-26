using Runiq.Teams.Execution.Planning;
using Runiq.Teams.Models.Teams;

namespace Runiq.Teams.Tests.Execution.Planning;

/// <summary>
/// Adaptif team execution planner davranışlarını doğrular.
/// </summary>
public sealed class AdaptiveTeamExecutionPlannerTests
{
    [Fact]
    public async Task CreatePlanAsync_ShouldUseValidModelJson()
    {
        var planner = CreatePlanner("""
        {
          "planningSummary": "Weather and final synthesis are enough.",
          "steps": [
            { "agentId": "weather-agent", "reason": "Outdoor comfort matters." },
            { "agentId": "planner-agent", "reason": "Final user answer is needed." }
          ],
          "finalAgentId": "planner-agent"
        }
        """);

        var plan = await planner.CreatePlanAsync(CreateTeam(), "Is walking comfortable?");

        Assert.Equal("Weather and final synthesis are enough.", plan.PlanningSummary);
        Assert.Equal("planner-agent", plan.FinalAgentId);
        Assert.Collection(
            plan.Steps,
            first =>
            {
                Assert.Equal("weather-agent", first.AgentId);
                Assert.Equal("Weather Analyst", first.Role);
                Assert.False(first.IsFinalMember);
            },
            second =>
            {
                Assert.Equal("planner-agent", second.AgentId);
                Assert.Equal("Travel Planner", second.Role);
                Assert.True(second.IsFinalMember);
            });
    }

    [Fact]
    public async Task CreatePlanAsync_ShouldIgnoreUnknownAgentIds()
    {
        var planner = CreatePlanner("""
        {
          "steps": [
            { "agentId": "invented-agent", "reason": "Not allowed." },
            { "agentId": "weather-agent", "reason": "Weather matters." }
          ],
          "finalAgentId": "weather-agent"
        }
        """);

        var plan = await planner.CreatePlanAsync(CreateTeam(), "Is walking comfortable?");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("weather-agent", step.AgentId);
        Assert.True(step.IsFinalMember);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"steps\":[]}")]
    public async Task CreatePlanAsync_ShouldFallbackToSequentialPlan_WhenModelOutputIsInvalid(string json)
    {
        var planner = CreatePlanner(json);

        var plan = await planner.CreatePlanAsync(CreateTeam(), "Plan a trip.");

        Assert.Equal("planner-agent", plan.FinalAgentId);
        Assert.Equal(
            ["weather-agent", "budget-agent", "places-agent", "planner-agent"],
            plan.Steps.Select(step => step.AgentId).ToArray());
        Assert.Contains("fallback", plan.PlanningSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePlanAsync_ShouldUseLastSelectedStepAsFinal_WhenFinalAgentIdIsMissing()
    {
        var planner = CreatePlanner("""
        {
          "steps": [
            { "agentId": "places-agent", "reason": "Historical places matter." },
            { "agentId": "planner-agent", "reason": "Final response is needed." }
          ]
        }
        """);

        var plan = await planner.CreatePlanAsync(CreateTeam(), "Plan historical places.");

        Assert.Equal("planner-agent", plan.FinalAgentId);
        Assert.True(plan.Steps[^1].IsFinalMember);
    }

    private static AdaptiveTeamExecutionPlanner CreatePlanner(string json)
    {
        return new AdaptiveTeamExecutionPlanner(
            new FakeTeamPlanningModelClient(json),
            new SequentialTeamExecutionPlanner());
    }

    private static AgentTeam CreateTeam()
    {
        return new AgentTeam(
                id: "travel-team",
                name: "Travel Team",
                instructions: "Create travel plans.")
            .UseAdaptiveMode()
            .AddMember("weather-agent", "Weather Analyst")
            .AddMember("budget-agent", "Budget Analyst")
            .AddMember("places-agent", "Places Researcher")
            .AddMember("planner-agent", "Travel Planner");
    }

    private sealed class FakeTeamPlanningModelClient : ITeamPlanningModelClient
    {
        private readonly string json;

        public FakeTeamPlanningModelClient(string json)
        {
            this.json = json;
        }

        public Task<string> CreatePlanJsonAsync(
            AgentTeam team,
            string userInput,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(json);
        }
    }
}
