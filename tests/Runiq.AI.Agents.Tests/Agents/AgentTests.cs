using Runiq.AI.Agents.Configuration;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class AgentTests
{
    [Fact]
    // Ensures the provider-neutral public constructor remains available after RAG API consolidation.
    public void Constructor_ShouldExposeProviderNeutralOverload()
    {
        var constructor = typeof(Agent).GetConstructor([
            typeof(string), typeof(string), typeof(string), typeof(string), typeof(string),
            typeof(Runiq.AI.Core.Configuration.ProviderOptions), typeof(string), typeof(string),
        ]);

        Assert.NotNull(constructor);
    }

    [Fact]
    // Ensures the single public RAG entry point stores normalized policy configuration.
    public void UseRag_ShouldConfigureAgent()
    {
        var agent = CreateAgent().UseRag(options =>
        {
            options.IndexName = " documents ";
            options.Mode = RagExecutionMode.Grounded;
            options.NoContextBehavior = RagNoContextBehavior.ReturnNotFound;
            options.MinimumRelevanceScore = 0.75;
        });

        Assert.True(agent.Rag!.Enabled);
        Assert.Equal("documents", agent.Rag.IndexName);
        Assert.Equal(RagExecutionMode.Grounded, agent.Rag.Mode);
        Assert.Equal(RagNoContextBehavior.ReturnNotFound, agent.Rag.NoContextBehavior);
        Assert.Equal(0.75, agent.Rag.MinimumRelevanceScore);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    // Ensures invalid static index configuration fails immediately.
    public void UseRag_ShouldRejectMissingIndex(string indexName)
    {
        Assert.Throws<ArgumentException>(() =>
            CreateAgent().UseRag(options => options.IndexName = indexName));
    }

    [Fact]
    // Ensures Required mode cannot be configured to fall back to unconstrained model knowledge.
    public void UseRag_ShouldRejectRequiredAnswerNormally()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateAgent().UseRag(options =>
            {
                options.IndexName = "documents";
                options.Mode = RagExecutionMode.Required;
                options.NoContextBehavior = RagNoContextBehavior.AnswerNormally;
            }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    // Ensures undefined execution mode values fail during configuration rather than at provider invocation.
    public void UseRag_ShouldRejectUndefinedExecutionMode(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAgent().UseRag(options =>
            {
                options.IndexName = "documents";
                options.Mode = (RagExecutionMode)value;
            }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    // Ensures undefined no-context behavior values fail during configuration.
    public void UseRag_ShouldRejectUndefinedNoContextBehavior(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAgent().UseRag(options =>
            {
                options.IndexName = "documents";
                options.NoContextBehavior = (RagNoContextBehavior)value;
            }));
    }

    [Theory]
    [InlineData(RagExecutionMode.Open, RagNoContextBehavior.AnswerNormally)]
    [InlineData(RagExecutionMode.Open, RagNoContextBehavior.ReturnNotFound)]
    [InlineData(RagExecutionMode.Open, RagNoContextBehavior.FailExecution)]
    [InlineData(RagExecutionMode.Grounded, RagNoContextBehavior.AnswerNormally)]
    [InlineData(RagExecutionMode.Grounded, RagNoContextBehavior.ReturnNotFound)]
    [InlineData(RagExecutionMode.Grounded, RagNoContextBehavior.FailExecution)]
    [InlineData(RagExecutionMode.Required, RagNoContextBehavior.ReturnNotFound)]
    [InlineData(RagExecutionMode.Required, RagNoContextBehavior.FailExecution)]
    // Ensures every non-contradictory mode and no-context combination remains valid.
    public void UseRag_ShouldAcceptSupportedPolicyMatrix(
        RagExecutionMode mode,
        RagNoContextBehavior noContextBehavior)
    {
        var agent = CreateAgent().UseRag(options =>
        {
            options.IndexName = "documents";
            options.Mode = mode;
            options.NoContextBehavior = noContextBehavior;
        });

        Assert.Equal(mode, agent.Rag!.Mode);
        Assert.Equal(noContextBehavior, agent.Rag.NoContextBehavior);
    }

    [Fact]
    // Ensures non-finite acceptance thresholds are rejected before execution.
    public void UseRag_ShouldRejectNonFiniteRelevanceThreshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAgent().UseRag(options =>
            {
                options.IndexName = "documents";
                options.MinimumRelevanceScore = double.NaN;
            }));
    }

    private static Agent CreateAgent() => new("agent", "Agent", "instructions", "openai/model", "key");
}
