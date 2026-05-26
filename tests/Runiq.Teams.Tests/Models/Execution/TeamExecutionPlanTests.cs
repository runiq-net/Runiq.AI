using Runiq.Teams.Models.Execution;

namespace Runiq.Teams.Tests.Models.Execution;

/// <summary>
/// Team execution plan doğrulama davranışlarını doğrular.
/// </summary>
public sealed class TeamExecutionPlanTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenStepsAreEmpty()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new TeamExecutionPlan(
                [],
                "planner-agent"));

        Assert.Equal("steps", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldPreserveFinalAgentIdAndPlanningSummary()
    {
        var step = new TeamExecutionPlanStep(
            agentId: "planner-agent",
            role: "Planner",
            reason: "Final response is needed.",
            order: 0,
            isFinalMember: true);

        var plan = new TeamExecutionPlan(
            [step],
            " planner-agent ",
            " Planning summary. ");

        Assert.Equal("planner-agent", plan.FinalAgentId);
        Assert.Equal("Planning summary.", plan.PlanningSummary);
        Assert.Same(step, plan.Steps[0]);
    }
}
