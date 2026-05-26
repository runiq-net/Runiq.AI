using Runiq.Agents;

namespace Runiq.Workflows.Tests;

public sealed class WorkflowAgentResolverTests
{
    /// <summary>
    /// Resolver'ın kayıtlı agent tipini doğru instance'a çözdüğünü doğrular.
    /// </summary>
    [Fact]
    public void Resolve_ShouldReturnRegisteredAgent_WhenAgentTypeExists()
    {
        var agent = new TestAgent();
        var resolver = new WorkflowAgentResolver([agent]);

        var resolved = resolver.Resolve(typeof(TestAgent));

        Assert.Same(agent, resolved);
    }

    /// <summary>
    /// Resolver'ın kayıtlı olmayan agent tipi için hata verdiğini doğrular.
    /// </summary>
    [Fact]
    public void Resolve_ShouldThrow_WhenAgentTypeDoesNotExist()
    {
        var resolver = new WorkflowAgentResolver([]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve(typeof(TestAgent)));

        Assert.Contains("No registered workflow agent found for type", exception.Message);
    }

    private sealed class TestAgent : Agent
    {
        public TestAgent()
            : base(
                id: "test-agent",
                name: "Test Agent",
                instructions: "Test instructions.",
                model: "openai/gpt-5")
        {
        }
    }
}