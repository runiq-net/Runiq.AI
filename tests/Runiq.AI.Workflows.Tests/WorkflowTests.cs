using Runiq.AI.Workflows.Infrastructure;
using Runiq.AI.Workflows.Domain;
using Runiq.AI.Workflows.Models;
using Runiq.AI.Agents;
using Runiq.AI.Workflows;

namespace Runiq.AI.Workflows.Tests;

public sealed class FlowTests
{
    /// <summary>
    /// Flow olusturulurken id ve ad bilgilerinin dogru saklandigini dogrular.
    /// </summary>
    [Fact]
    public void Constructor_ShouldStoreFlowMetadata()
    {
        var workflow = new Flow(
            id: "travel-planning-workflow",
            name: "Travel Planning Flow",
            instructions: "Creates practical travel plans.");

        Assert.Equal("travel-planning-workflow", workflow.Id);
        Assert.Equal("Travel Planning Flow", workflow.Name);
        Assert.Equal("Creates practical travel plans.", workflow.Instructions);
    }

    /// <summary>
    /// Step<T> çagrisinin workflow içine çalistirilabilir bir adim ekledigini dogrular.
    /// </summary>
    [Fact]
    public void Step_ShouldAddFlowStep()
    {

        var workflow = new Flow("travel", "Travel")
            .Step<TestAgent>("begin")
                .OnSuccessEnd()
                .OnFailureStop()
            .Build();

        var step = Assert.Single(workflow.Steps);

        Assert.Equal("begin", step.Id);
        Assert.Equal(typeof(TestAgent), step.ExecutableType);

        Assert.Equal(FailureBehavior.Stop, step.FailureBehavior);
    }

    /// <summary>
    /// Basari ve hata geçislerinin fluent API üzerinden dogru tanimlandigini dogrular.
    /// </summary>
    [Fact]
    public void StepBuilder_ShouldStoreSuccessAndFailureTransitions()
    {
        var workflow = new Flow("travel", "Travel")
             .Step<TestAgent>("weather")
                 .OnSuccess("places")
                 .OnFailureGoTo("fallback")
             .Build();

        var step = Assert.Single(workflow.Steps);

        Assert.Equal("places", step.SuccessStepId);
        Assert.Equal(FailureBehavior.GoTo, step.FailureBehavior);
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
