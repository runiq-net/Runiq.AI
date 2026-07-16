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
        Assert.Null(options.MinimumRelevanceScore);
        Assert.Null(options.IndexName);
    }
}
