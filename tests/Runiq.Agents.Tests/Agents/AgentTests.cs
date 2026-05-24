using Runiq.Agents;

namespace Runiq.Agents.Tests.Agents;

public sealed class AgentTests
{
    [Fact]
    public void UseContextSpace_ShouldAttachContextSpaceId()
    {
        // Agent'a context space id bağlanabildiğini doğrular.
        var agent = CreateAgent();

        var result = agent.UseContextSpace("travel-planning");

        Assert.Same(agent, result);

        var contextSpaceId = Assert.Single(agent.ContextSpaceIds);
        Assert.Equal("travel-planning", contextSpaceId);
    }

    [Fact]
    public void UseContextSpace_ShouldTrimContextSpaceId()
    {
        // Context space id değerinin agent üzerinde normalize edilerek saklandığını doğrular.
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
        // Boş context space id değerinin agent'a bağlanamayacağını doğrular.
        var agent = CreateAgent();

        var exception = Assert.Throws<ArgumentException>(() =>
            agent.UseContextSpace(contextSpaceId));

        Assert.Equal("contextSpaceId", exception.ParamName);
    }

    [Fact]
    public void UseContextSpace_ShouldThrow_WhenContextSpaceIdAlreadyExistsIgnoringCase()
    {
        // Aynı context space id değerinin case-insensitive olarak ikinci kez bağlanamayacağını doğrular.
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