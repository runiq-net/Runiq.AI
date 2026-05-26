using Runiq.Agents;
using Runiq.Workflows.Tests.Fakes;

namespace Runiq.Workflows.Tests;

public sealed class WorkflowExecutionRuntimeTests
{
    /// <summary>
    /// Runtime'ın geçersiz workflow tanımında başarısız sonuç döndürdüğünü doğrular.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailedResult_WhenWorkflowIsInvalid()
    {
        var runtime = new WorkflowExecutionRuntime(
            new WorkflowAgentResolver([]),
            new FakeWorkflowAgentExecutor());

        var workflow = new Workflow("invalid", "Invalid");

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "test input");

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Status);
        Assert.Contains("Workflow must contain at least one step.", result.ErrorMessage);
        Assert.Empty(result.StepResults);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFollowSuccessPath()
    {
        var runtime = new WorkflowExecutionRuntime(
            new WorkflowAgentResolver([new TestAgent()]),
            new FakeWorkflowAgentExecutor());

        var workflow = new Workflow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnSuccess("places")
                .OnFailureStop()
            .Step<TestAgent>("places")
                .OnSuccess("planner")
                .OnFailureStop()
            .Step<TestAgent>("planner")
                .OnFailureStop()
            .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        Assert.Equal(3, result.StepResults.Count);

        Assert.Equal("weather", result.StepResults[0].StepId);
        Assert.Equal("places", result.StepResults[1].StepId);
        Assert.Equal("planner", result.StepResults[2].StepId);
    }

    /// <summary>
    /// Runtime'ın geçerli workflow tanımında ilk adımı çalıştırma sonucunu döndürdüğünü doğrular.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldReturnCompletedResult_WithFirstStepResult_WhenWorkflowIsValid()
    {
        var runtime = new WorkflowExecutionRuntime(
            new WorkflowAgentResolver([new TestAgent()]),
            new FakeWorkflowAgentExecutor());

        var workflow = new Workflow("valid", "Valid")
            .Step<TestAgent>("begin")
                .OnFailureStop()
            .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        Assert.Equal(WorkflowExecutionStatus.Completed, result.Status);

        Assert.Equal("hello", result.FinalOutput);

        var stepResult = Assert.Single(result.StepResults);

        Assert.Equal("begin", stepResult.StepId);
        Assert.Equal(typeof(TestAgent), stepResult.AgentType);
        Assert.Equal(WorkflowStepExecutionStatus.Completed, stepResult.Status);
        Assert.Equal("hello", stepResult.Output);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStop_WhenFailureBehaviorIsStop()
    {
        var runtime = new WorkflowExecutionRuntime(
            new WorkflowAgentResolver([new TestAgent()]),
            new FakeWorkflowAgentExecutor());

        var workflow = new Workflow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnSuccess("places")
                .OnFailureStop()
            .Step<TestAgent>("places")
                .OnFailureStop()
            .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

         Assert.Equal(WorkflowExecutionStatus.Completed, result.Status);

    }

    [Fact]
    public async Task ExecuteAsync_ShouldStop_WhenAgentFails_AndFailureBehaviorIsStop()
    {
        var executor = new FakeWorkflowAgentExecutor()
            .WithFailure("test-agent");

        var runtime = new WorkflowExecutionRuntime(
            new WorkflowAgentResolver([new TestAgent()]),
            executor);

        var workflow = new Workflow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnFailureStop()
            .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Status);

        var stepResult = Assert.Single(result.StepResults);

        Assert.Equal("weather", stepResult.StepId);
        Assert.Equal(
            WorkflowStepExecutionStatus.Failed,
            stepResult.Status);
    }

    /// <summary>
    /// Bir adım hata verdiğinde OnFailureContinue tanımlıysa workflow'un belirtilen adıma devam ettiğini doğrular.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldContinueToConfiguredStep_WhenAgentFails_AndFailureBehaviorIsContinue()
    {
        var executor = new FakeWorkflowAgentExecutor()
            .WithFailure("test-agent");

        var runtime = new WorkflowExecutionRuntime(
            new WorkflowAgentResolver([new TestAgent(), new PlannerAgent()]),
            executor);

        var workflow = new Workflow("travel", "Travel")
           .Step<TestAgent>("weather")
               .OnFailureContinue("planner")
           .Step<PlannerAgent>("planner")
               .OnFailureStop()
           .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        Assert.Equal(WorkflowExecutionStatus.Completed, result.Status);

       
        Assert.Equal(2, result.StepResults.Count);

        Assert.Equal("weather", result.StepResults[0].StepId);
        Assert.Equal(WorkflowStepExecutionStatus.Failed, result.StepResults[0].Status);

        Assert.Equal("planner", result.StepResults[1].StepId);
        Assert.Equal(WorkflowStepExecutionStatus.Completed, result.StepResults[1].Status);
    }

    /// <summary>
    /// Bir adım hata verdiğinde OnFailureGoTo tanımlıysa workflow'un fallback adıma yönlendiğini doğrular.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldGoToConfiguredStep_WhenAgentFails_AndFailureBehaviorIsGoTo()
    {
        var executor = new FakeWorkflowAgentExecutor()
            .WithFailure("test-agent");

        var runtime = new WorkflowExecutionRuntime(
            new WorkflowAgentResolver([new TestAgent(), new FallbackAgent()]),
            executor);

        var workflow = new Workflow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnFailureGoTo("fallback")
            .Step<FallbackAgent>("fallback")
                .OnFailureStop()
            .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        Assert.Equal(WorkflowExecutionStatus.Completed, result.Status);
        Assert.Equal(2, result.StepResults.Count);

        Assert.Equal("weather", result.StepResults[0].StepId);
        Assert.Equal(WorkflowStepExecutionStatus.Failed, result.StepResults[0].Status);

        Assert.Equal("fallback", result.StepResults[1].StepId);
        Assert.Equal(WorkflowStepExecutionStatus.Completed, result.StepResults[1].Status);
    }

    private sealed class FallbackAgent : Agent
    {
        public FallbackAgent()
            : base(
                id: "fallback-agent",
                name: "Fallback Agent",
                instructions: "Fallback instructions.",
                model: "openai/gpt-5")
        {
        }
    }

    private sealed class PlannerAgent : Agent
    {
        public PlannerAgent()
            : base(
                id: "planner-agent",
                name: "Planner Agent",
                instructions: "Planner instructions.",
                model: "openai/gpt-5")
        {
        }
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