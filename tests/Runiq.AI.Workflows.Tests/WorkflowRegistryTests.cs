using Runiq.AI.Workflows.Infrastructure;
using Runiq.AI.Workflows.Domain;
using Runiq.AI.Workflows.Models;
using Runiq.AI.Agents;

namespace Runiq.AI.Workflows.Tests;

public sealed class FlowCatalogTests
{
    /// <summary>
    /// Registry'ye eklenen workflow taniminin listede yer aldigini dogrular.
    /// </summary>
    [Fact]
    public void AddFlow_ShouldRegisterFlow()
    {
        var workflow = CreateFlow("travel");
        var registry = new FlowCatalog();

        registry.AddFlow(workflow);

        var registeredFlow = Assert.Single(registry.Flows);

        Assert.Same(workflow, registeredFlow);
    }

    /// <summary>
    /// Registry'nin workflow tanimini id degerine göre buldugunu dogrular.
    /// </summary>
    [Fact]
    public void FindById_ShouldReturnFlow_WhenFlowExists()
    {
        var workflow = CreateFlow("travel");
        var registry = new FlowCatalog()
            .AddFlow(workflow);

        var result = registry.FindById("travel");

        Assert.Same(workflow, result);
    }

    /// <summary>
    /// Registry'nin ayni workflow id ile ikinci kayit eklenmesini engelledigini dogrular.
    /// </summary>
    [Fact]
    public void AddFlow_ShouldThrow_WhenFlowIdAlreadyExists()
    {
        var registry = new FlowCatalog()
            .AddFlow(CreateFlow("travel"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.AddFlow(CreateFlow("TRAVEL")));

        Assert.Contains("Flow with id 'TRAVEL' is already registered.", exception.Message);
    }

    private static Flow CreateFlow(string id)
    {
        return new Flow(id, "Test Flow")
            .Step<TestAgent>("begin")
                .OnFailureStop()
            .Build();
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
