using Runiq.Agents;

namespace Runiq.Agents.Tests.Agents;

public sealed class AgentTests
{
    // Verifies that an agent can attach a context space id and return itself for chaining.
    [Fact]
    public void UseContextSpace_ShouldAttachContextSpaceId()
    {
        var agent = CreateAgent();

        var result = agent.UseContextSpace("travel-planning");

        Assert.Same(agent, result);

        var contextSpaceId = Assert.Single(agent.ContextSpaceIds);
        Assert.Equal("travel-planning", contextSpaceId);
    }

    // Verifies that context space ids are trimmed before being stored on the agent.
    [Fact]
    public void UseContextSpace_ShouldTrimContextSpaceId()
    {
        var agent = CreateAgent();

        agent.UseContextSpace(" travel-planning ");

        var contextSpaceId = Assert.Single(agent.ContextSpaceIds);
        Assert.Equal("travel-planning", contextSpaceId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UseContextSpace_ShouldThrow_WhenContextSpaceIdIsEmpty(string contextSpaceId)
    {
        // Verifies that empty context space ids cannot be attached to an agent.
        var agent = CreateAgent();

        var exception = Assert.Throws<ArgumentException>(() =>
            agent.UseContextSpace(contextSpaceId));

        Assert.Equal("contextSpaceId", exception.ParamName);
    }

    // Verifies that duplicate context space ids are rejected with case-insensitive comparison.
    [Fact]
    public void UseContextSpace_ShouldThrow_WhenContextSpaceIdAlreadyExistsIgnoringCase()
    {
        var agent = CreateAgent();

        agent.UseContextSpace("travel-planning");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            agent.UseContextSpace("TRAVEL-PLANNING"));

        Assert.Contains("travel-planning", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Agent CreateAgent()
    {
        return new Agent(
            id: "travel-agent",
            name: "Travel Agent",
            instructions: "Plan short travel routes.",
            model: "openai/gpt-5");
    }
}
