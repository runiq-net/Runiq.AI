using Runiq.Agents;

namespace Runiq.Agents.Tests.Agents;

public sealed class AgentExecutionEventTests
{
    // Verifies that the skill loaded factory creates an event with the expected skill metadata.
    [Fact]
    public void SkillLoaded_ShouldCreateSkillLoadedEvent()
    {
        var executionEvent = AgentExecutionEvent.SkillLoaded([
            new AgentExecutionLoadedSkillInfo(
                SkillId: "travel-planning",
                SkillName: "Travel Planning Skill",
                Version: "1.0.0",
                Description: "Travel behavior instructions.")
        ]);

        Assert.Equal(AgentExecutionEventKind.SkillLoaded, executionEvent.Kind);
        Assert.Null(executionEvent.Content);

        var skill = Assert.Single(executionEvent.LoadedSkills!);

        Assert.Equal("travel-planning", skill.SkillId);
        Assert.Equal("Travel Planning Skill", skill.SkillName);
        Assert.Equal("1.0.0", skill.Version);
        Assert.Equal("Travel behavior instructions.", skill.Description);
    }
}
