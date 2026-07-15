using Runiq.AI.Agents.Configuration;

namespace Runiq.AI.Agents.Tests.Configuration;

public sealed class AgentRagOptionsTests
{
    // Ensures RAG defaults to enabled, fail-closed execution.
    [Fact]
    public void Defaults_ShouldRequireRetrieval()
    {
        var options = new AgentRagOptions();

        Assert.True(options.Enabled);
        Assert.Equal(RagExecutionMode.Required, options.Mode);
        Assert.Null(options.IndexName);
    }
}
