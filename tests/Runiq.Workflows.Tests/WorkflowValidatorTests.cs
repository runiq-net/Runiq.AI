using Runiq.Agents;

namespace Runiq.Workflows.Tests;

public sealed class WorkflowValidatorTests
{
    /// <summary>
    /// En az bir bitiş adımı olan geçerli workflow tanımının başarılı doğrulandığını doğrular.
    /// </summary>
    [Fact]
    public void Validate_ShouldReturnSuccess_WhenWorkflowIsValid()
    {
        var workflow = new Workflow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnSuccess("planner")
                .OnFailureStop()
            .Step<TestAgent>("planner")
                .OnSuccessEnd()
                .OnFailureStop()
            .Build();

        var result = WorkflowValidator.Validate(workflow);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// Hiç adımı olmayan workflow tanımının geçersiz olduğunu doğrular.
    /// </summary>
    [Fact]
    public void Validate_ShouldReturnFailure_WhenWorkflowHasNoSteps()
    {
        var workflow = new Workflow("empty", "Empty");

        var result = WorkflowValidator.Validate(workflow);

        Assert.False(result.IsValid);
        Assert.Contains("Workflow must contain at least one step.", result.Errors);
    }

    /// <summary>
    /// Aynı step id birden fazla kullanıldığında doğrulamanın hata döndürdüğünü doğrular.
    /// </summary>
    [Fact]
    public void Validate_ShouldReturnFailure_WhenWorkflowHasDuplicateStepIds()
    {
        var workflow = new Workflow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnSuccessEnd()
                .OnFailureStop()
            .Step<TestAgent>("weather")
                .OnSuccessEnd()
                .OnFailureStop()
            .Build();

        var result = WorkflowValidator.Validate(workflow);

        Assert.False(result.IsValid);
        Assert.Contains("Workflow contains duplicate step id 'weather'.", result.Errors);
    }

    /// <summary>
    /// Başarı geçişi bilinmeyen bir adıma işaret ettiğinde doğrulamanın hata döndürdüğünü doğrular.
    /// </summary>
    [Fact]
    public void Validate_ShouldReturnFailure_WhenSuccessTargetIsUnknown()
    {
        var workflow = new Workflow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnSuccess("missing-step")
                .OnFailureStop()
            .Build();

        var result = WorkflowValidator.Validate(workflow);

        Assert.False(result.IsValid);
        Assert.Contains("Step 'weather' has unknown success target 'missing-step'.", result.Errors);
    }

    /// <summary>
    /// Hata geçişi bilinmeyen bir adıma işaret ettiğinde doğrulamanın hata döndürdüğünü doğrular.
    /// </summary>
    [Fact]
    public void Validate_ShouldReturnFailure_WhenFailureTargetIsUnknown()
    {
        var workflow = new Workflow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnSuccessEnd()
                .OnFailureGoTo("missing-fallback")
            .Build();

        var result = WorkflowValidator.Validate(workflow);

        Assert.False(result.IsValid);
        Assert.Contains("Step 'weather' has unknown failure target 'missing-fallback'.", result.Errors);
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