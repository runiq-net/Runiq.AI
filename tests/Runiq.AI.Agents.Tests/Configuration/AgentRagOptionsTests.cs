using Runiq.AI.Agents.Configuration;

namespace Runiq.AI.Agents.Tests.Configuration;

public sealed class AgentRagOptionsTests
{
    [Fact]
    // Ensures RAG defaults preserve existing normal-answer behavior unless a stricter policy is selected.
    public void Defaults_ShouldUseOpenNormalAnswerPolicy()
    {
        var options = new AgentRagOptions();

        Assert.True(options.Enabled);
        Assert.Equal(RagExecutionMode.Open, options.Mode);
        Assert.Equal(RagNoContextBehavior.AnswerNormally, options.NoContextBehavior);
        Assert.Null(options.Acceptance.MinimumRelevance);
        Assert.Equal(20, options.Acceptance.CandidateCount);
        Assert.Equal(5, options.Acceptance.MaximumAcceptedResults);
        Assert.Null(options.Acceptance.ProviderSpecificAcceptance);
        Assert.Null(options.IndexName);
        Assert.Equal(32_768, options.ContextBudget.MaximumContextTokens);
        Assert.Equal(4_096, options.ContextBudget.ResponseTokenReserve);
        Assert.Equal(int.MaxValue, options.ContextBudget.MaximumChunksPerSource);
        Assert.False(options.ContextBudget.PreferSourceDiversity);
    }

    // Ensures invalid maximum context budgets fail before retrieval or provider work can begin.
    [Fact]
    public void Validate_ShouldRejectNonPositiveMaximumContextTokens()
    {
        var options = new AgentRagOptions { IndexName = "documents" };
        options.ContextBudget.MaximumContextTokens = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() => AgentRagPolicyValidator.Validate(options, requireIndex: true));
    }

    // Ensures response reservation cannot consume the entire configured model context.
    [Fact]
    public void Validate_ShouldRejectResponseReserveAtMaximumContext()
    {
        var options = new AgentRagOptions { IndexName = "documents" };
        options.ContextBudget.MaximumContextTokens = 100;
        options.ContextBudget.ResponseTokenReserve = 100;

        Assert.Throws<ArgumentOutOfRangeException>(() => AgentRagPolicyValidator.Validate(options, requireIndex: true));
    }

    // Ensures per-source selection is always configured with a positive bound.
    [Fact]
    public void Validate_ShouldRejectNonPositiveSourceLimit()
    {
        var options = new AgentRagOptions { IndexName = "documents" };
        options.ContextBudget.MaximumChunksPerSource = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() => AgentRagPolicyValidator.Validate(options, requireIndex: true));
    }
}
