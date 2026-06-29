using Runiq.Agents;

namespace Runiq.Agents.Tests.Agents;

public sealed class AgentTests
{
    [Fact]
    public void Constructor_ShouldExposeLegacyPublicOverload()
    {
        var constructor = typeof(Agent).GetConstructor([
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(Runiq.Agents.Configuration.ProviderOptions),
            typeof(string),
            typeof(string)
        ]);

        Assert.NotNull(constructor);
    }

    [Fact]
    public void Constructor_ShouldExposeRagOptionsOverload()
    {
        var constructor = typeof(Agent).GetConstructor([
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(Runiq.Agents.Configuration.ProviderOptions),
            typeof(string),
            typeof(string),
            typeof(Runiq.Agents.Configuration.AgentRagOptions)
        ]);

        Assert.NotNull(constructor);

        var agent = new Agent(
            id: "travel-agent",
            name: "Travel Agent",
            instructions: "Plan short travel routes.",
            model: "ollama/llama3",
            rag: new Runiq.Agents.Configuration.AgentRagOptions { IndexName = "documents" });

        Assert.NotNull(agent.Rag);
        Assert.Equal("documents", agent.Rag.IndexName);
    }

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

    [Fact]
    public void UseRagIndex_ShouldConfigureAgentRagIndexName()
    {
        var agent = CreateAgent();

        var result = agent.UseRagIndex(" documents ");

        Assert.Same(agent, result);
        Assert.NotNull(agent.Rag);
        Assert.Equal("documents", agent.Rag.IndexName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UseRagIndex_ShouldThrow_WhenIndexNameIsEmpty(string indexName)
    {
        var agent = CreateAgent();

        var exception = Assert.Throws<ArgumentException>(() =>
            agent.UseRagIndex(indexName));

        Assert.Equal("indexName", exception.ParamName);
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
