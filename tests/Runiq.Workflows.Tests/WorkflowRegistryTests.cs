using Runiq.Agents;

namespace Runiq.Workflows.Tests;

public sealed class WorkflowRegistryTests
{
    /// <summary>
    /// Registry'ye eklenen workflow tanımının listede yer aldığını doğrular.
    /// </summary>
    [Fact]
    public void AddWorkflow_ShouldRegisterWorkflow()
    {
        var workflow = CreateWorkflow("travel");
        var registry = new WorkflowRegistry();

        registry.AddWorkflow(workflow);

        var registeredWorkflow = Assert.Single(registry.Workflows);

        Assert.Same(workflow, registeredWorkflow);
    }

    /// <summary>
    /// Registry'nin workflow tanımını id değerine göre bulduğunu doğrular.
    /// </summary>
    [Fact]
    public void FindById_ShouldReturnWorkflow_WhenWorkflowExists()
    {
        var workflow = CreateWorkflow("travel");
        var registry = new WorkflowRegistry()
            .AddWorkflow(workflow);

        var result = registry.FindById("travel");

        Assert.Same(workflow, result);
    }

    /// <summary>
    /// Registry'nin aynı workflow id ile ikinci kayıt eklenmesini engellediğini doğrular.
    /// </summary>
    [Fact]
    public void AddWorkflow_ShouldThrow_WhenWorkflowIdAlreadyExists()
    {
        var registry = new WorkflowRegistry()
            .AddWorkflow(CreateWorkflow("travel"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.AddWorkflow(CreateWorkflow("TRAVEL")));

        Assert.Contains("Workflow with id 'TRAVEL' is already registered.", exception.Message);
    }

    private static Workflow CreateWorkflow(string id)
    {
        return new Workflow(id, "Test Workflow")
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