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
    }
}
