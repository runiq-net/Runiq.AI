using Runiq.AI.Agents.Configuration;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class AgentTests
{
    // Ensures the provider-neutral public constructor remains available after RAG API consolidation.
    [Fact]
    public void Constructor_ShouldExposeProviderNeutralOverload()
    {
        var constructor = typeof(Agent).GetConstructor([
            typeof(string), typeof(string), typeof(string), typeof(string), typeof(string),
            typeof(Runiq.AI.Core.Configuration.ProviderOptions), typeof(string), typeof(string),
        ]);

        Assert.NotNull(constructor);
    }

    // Ensures the single public RAG entry point stores normalized configuration.
    [Fact]
    public void UseRag_ShouldConfigureAgent()
    {
        var agent = CreateAgent().UseRag(options =>
        {
            options.IndexName = " documents ";
            options.Mode = RagExecutionMode.Optional;
        });

        Assert.True(agent.Rag!.Enabled);
        Assert.Equal("documents", agent.Rag.IndexName);
        Assert.Equal(RagExecutionMode.Optional, agent.Rag.Mode);
    }

    // Ensures invalid static index configuration fails immediately.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UseRag_ShouldRejectMissingIndex(string indexName)
    {
        Assert.Throws<ArgumentException>(() =>
            CreateAgent().UseRag(options => options.IndexName = indexName));
    }

    private static Agent CreateAgent() => new("agent", "Agent", "instructions", "openai/model", "key");
}
