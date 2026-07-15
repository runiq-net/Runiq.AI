using Runiq.AI.Workflows.Services;
using Runiq.AI.Workflows.Infrastructure;
using Runiq.AI.Workflows.Domain;
using Runiq.AI.Workflows.Models;
using Runiq.AI.Agents;

namespace Runiq.AI.Workflows.Tests;

public sealed class RegisteredAgentStepResolverTests
{
    /// <summary>
    /// Resolver'in kayitli agent tipini dogru instance'a çözdügünü dogrular.
    /// </summary>
    [Fact]
    public void Resolve_ShouldReturnRegisteredAgent_WhenAgentTypeExists()
    {
        var agent = new TestAgent();
        var resolver = new RegisteredAgentStepResolver([agent]);

        var resolved = resolver.Resolve(typeof(TestAgent));

        Assert.Same(agent, resolved);
    }

    /// <summary>
    /// Resolver'in kayitli olmayan agent tipi için hata verdigini dogrular.
    /// </summary>
    [Fact]
    public void Resolve_ShouldThrow_WhenAgentTypeDoesNotExist()
    {
        var resolver = new RegisteredAgentStepResolver([]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => resolver.Resolve(typeof(TestAgent)));

        Assert.Contains("No registered flow agent found for type", exception.Message);
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
