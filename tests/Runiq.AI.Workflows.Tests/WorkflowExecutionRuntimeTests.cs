using Runiq.AI.Workflows.Services;
using Runiq.AI.Workflows.Infrastructure;
using Runiq.AI.Workflows.Domain;
using Runiq.AI.Workflows.Models;
using Runiq.AI.Agents;
using Runiq.AI.Workflows.Tests.Fakes;

namespace Runiq.AI.Workflows.Tests;

public sealed class FlowRunnerTests
{
    /// <summary>
    /// Runtime'in geçersiz workflow taniminda basarisiz sonuç döndürdügünü dogrular.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailedResult_WhenFlowIsInvalid()
    {
        var runtime = new FlowRunner(
            new RegisteredAgentStepResolver([]),
            new FakeRuniqAgentStepExecutor());

        var workflow = new Flow("invalid", "Invalid");

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "test input");

        Assert.Equal(RunStatus.Failed, result.Status);
        Assert.Contains("Flow must contain at least one step.", result.ErrorMessage);
        Assert.Empty(result.StepResults);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFollowSuccessPath()
    {
        var runtime = new FlowRunner(
            new RegisteredAgentStepResolver([new TestAgent()]),
            new FakeRuniqAgentStepExecutor());

        var workflow = new Flow("travel", "Travel")
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
    /// Runtime'in geçerli workflow taniminda ilk adimi çalistirma sonucunu döndürdügünü dogrular.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldReturnCompletedResult_WithFirstStepResult_WhenFlowIsValid()
    {
        var runtime = new FlowRunner(
            new RegisteredAgentStepResolver([new TestAgent()]),
            new FakeRuniqAgentStepExecutor());

        var workflow = new Flow("valid", "Valid")
            .Step<TestAgent>("begin")
                .OnFailureStop()
            .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        Assert.Equal(RunStatus.Completed, result.Status);

        Assert.Equal("hello", result.FinalOutput);

        var stepResult = Assert.Single(result.StepResults);

        Assert.Equal("begin", stepResult.StepId);
        Assert.Equal(typeof(TestAgent), stepResult.AgentType);
        Assert.Equal(StepRunStatus.Completed, stepResult.Status);
        Assert.Equal("hello", stepResult.Output);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeToolCalls_WhenAgentStepUsesTools()
    {
        // Agent adimi içindeki gerçek tool trace bilgisinin workflow step sonucuna tasindigini dogrular.
        var startedAt = DateTimeOffset.UtcNow;
        var completedAt = startedAt.AddMilliseconds(42);
        var executor = new FakeRuniqAgentStepExecutor()
            .WithToolCalls(
                "test-agent",
                [
                    new ToolCallRunResult(
                        toolCallId: "call-1",
                        toolName: "weather.lookup",
                        status: ToolCallRunStatus.Completed,
                        argumentsJson: """{"city":"Istanbul"}""",
                        outputJson: """{"condition":"Cloudy"}""",
                        startedAt: startedAt,
                        completedAt: completedAt)
                ]);

        var runtime = new FlowRunner(
            new RegisteredAgentStepResolver([new TestAgent()]),
            executor);

        var workflow = new Flow("valid", "Valid")
            .Step<TestAgent>("weather")
                .OnFailureStop()
            .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        var stepResult = Assert.Single(result.StepResults);
        var toolCall = Assert.Single(stepResult.ToolCalls);

        Assert.Equal("call-1", toolCall.ToolCallId);
        Assert.Equal("weather.lookup", toolCall.ToolName);
        Assert.Equal(ToolCallRunStatus.Completed, toolCall.Status);
        Assert.Equal("""{"city":"Istanbul"}""", toolCall.ArgumentsJson);
        Assert.Equal("""{"condition":"Cloudy"}""", toolCall.OutputJson);
        Assert.Equal(42, toolCall.DurationMs);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeFailedToolCalls_WhenAgentStepFails()
    {
        // Agent basarisiz olsa bile tool hata trace bilgisinin kaybolmadigini dogrular.
        var executor = new FakeRuniqAgentStepExecutor()
            .WithFailure("test-agent")
            .WithToolCalls(
                "test-agent",
                [
                    new ToolCallRunResult(
                        toolCallId: "call-1",
                        toolName: "places.search",
                        status: ToolCallRunStatus.Failed,
                        argumentsJson: """{"query":"museum"}""",
                        errorCode: "ToolFailed",
                        errorMessage: "Search failed.")
                ]);

        var runtime = new FlowRunner(
            new RegisteredAgentStepResolver([new TestAgent()]),
            executor);

        var workflow = new Flow("valid", "Valid")
            .Step<TestAgent>("places")
                .OnFailureStop()
            .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        var stepResult = Assert.Single(result.StepResults);
        var toolCall = Assert.Single(stepResult.ToolCalls);

        Assert.Equal(RunStatus.Failed, result.Status);
        Assert.Equal(ToolCallRunStatus.Failed, toolCall.Status);
        Assert.Equal("ToolFailed", toolCall.ErrorCode);
        Assert.Equal("Search failed.", toolCall.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStop_WhenFailureBehaviorIsStop()
    {
        var runtime = new FlowRunner(
            new RegisteredAgentStepResolver([new TestAgent()]),
            new FakeRuniqAgentStepExecutor());

        var workflow = new Flow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnSuccess("places")
                .OnFailureStop()
            .Step<TestAgent>("places")
                .OnFailureStop()
            .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        Assert.Equal(RunStatus.Completed, result.Status);

    }

    [Fact]
    public async Task ExecuteAsync_ShouldStop_WhenAgentFails_AndFailureBehaviorIsStop()
    {
        var executor = new FakeRuniqAgentStepExecutor()
            .WithFailure("test-agent");

        var runtime = new FlowRunner(
            new RegisteredAgentStepResolver([new TestAgent()]),
            executor);

        var workflow = new Flow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnFailureStop()
            .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        Assert.Equal(RunStatus.Failed, result.Status);

        var stepResult = Assert.Single(result.StepResults);

        Assert.Equal("weather", stepResult.StepId);
        Assert.Equal(
            StepRunStatus.Failed,
            stepResult.Status);
    }

    /// <summary>
    /// Bir adim hata verdiginde OnFailureContinue tanimliysa workflow'un belirtilen adima devam ettigini dogrular.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldContinueToConfiguredStep_WhenAgentFails_AndFailureBehaviorIsContinue()
    {
        var executor = new FakeRuniqAgentStepExecutor()
            .WithFailure("test-agent");

        var runtime = new FlowRunner(
            new RegisteredAgentStepResolver([new TestAgent(), new PlannerAgent()]),
            executor);

        var workflow = new Flow("travel", "Travel")
           .Step<TestAgent>("weather")
               .OnFailureContinue("planner")
           .Step<PlannerAgent>("planner")
               .OnFailureStop()
           .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        Assert.Equal(RunStatus.Completed, result.Status);


        Assert.Equal(2, result.StepResults.Count);

        Assert.Equal("weather", result.StepResults[0].StepId);
        Assert.Equal(StepRunStatus.Failed, result.StepResults[0].Status);

        Assert.Equal("planner", result.StepResults[1].StepId);
        Assert.Equal(StepRunStatus.Completed, result.StepResults[1].Status);
    }

    /// <summary>
    /// Bir adim hata verdiginde OnFailureGoTo tanimliysa workflow'un fallback adima yönlendigini dogrular.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldGoToConfiguredStep_WhenAgentFails_AndFailureBehaviorIsGoTo()
    {
        var executor = new FakeRuniqAgentStepExecutor()
            .WithFailure("test-agent");

        var runtime = new FlowRunner(
            new RegisteredAgentStepResolver([new TestAgent(), new FallbackAgent()]),
            executor);

        var workflow = new Flow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnFailureGoTo("fallback")
            .Step<FallbackAgent>("fallback")
                .OnFailureStop()
            .Build();

        var result = await runtime.ExecuteAsync(
            workflow,
            input: "hello");

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal(2, result.StepResults.Count);

        Assert.Equal("weather", result.StepResults[0].StepId);
        Assert.Equal(StepRunStatus.Failed, result.StepResults[0].Status);

        Assert.Equal("fallback", result.StepResults[1].StepId);
        Assert.Equal(StepRunStatus.Completed, result.StepResults[1].Status);
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

