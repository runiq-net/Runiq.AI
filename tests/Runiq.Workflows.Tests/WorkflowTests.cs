using Runiq.Agents;
using Runiq.Workflows;

namespace Runiq.Workflows.Tests;

public sealed class WorkflowTests
{
    /// <summary>
    /// Workflow oluşturulurken id ve ad bilgilerinin doğru saklandığını doğrular.
    /// </summary>
    [Fact]
    public void Constructor_ShouldStoreWorkflowMetadata()
    {
        var workflow = new Workflow(
            id: "travel-planning-workflow",
            name: "Travel Planning Workflow",
            instructions: "Creates practical travel plans.");

        Assert.Equal("travel-planning-workflow", workflow.Id);
        Assert.Equal("Travel Planning Workflow", workflow.Name);
        Assert.Equal("Creates practical travel plans.", workflow.Instructions);
    }

    /// <summary>
    /// Step<T> çağrısının workflow içine çalıştırılabilir bir adım eklediğini doğrular.
    /// </summary>
    [Fact]
    public void Step_ShouldAddWorkflowStep()
    {

        var workflow = new Workflow("travel", "Travel")
            .Step<TestAgent>("begin")
                .OnSuccessEnd()
                .OnFailureStop()
            .Build();

        var step = Assert.Single(workflow.Steps);

        Assert.Equal("begin", step.Id);
        Assert.Equal(typeof(TestAgent), step.ExecutableType);
 
        Assert.Equal(WorkflowFailureBehavior.Stop, step.FailureBehavior);
    }

    /// <summary>
    /// Başarı ve hata geçişlerinin fluent API üzerinden doğru tanımlandığını doğrular.
    /// </summary>
    [Fact]
    public void StepBuilder_ShouldStoreSuccessAndFailureTransitions()
    {
        var workflow = new Workflow("travel", "Travel")
             .Step<TestAgent>("weather")
                 .OnSuccess("places")
                 .OnFailureGoTo("fallback")
             .Build();

        var step = Assert.Single(workflow.Steps);

        Assert.Equal("places", step.SuccessStepId);
        Assert.Equal(WorkflowFailureBehavior.GoTo, step.FailureBehavior);
        Assert.Equal("fallback", step.FailureStepId);
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