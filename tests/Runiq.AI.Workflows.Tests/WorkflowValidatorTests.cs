using Runiq.AI.Workflows.Validations;
using Runiq.AI.Workflows.Infrastructure;
using Runiq.AI.Workflows.Domain;
using Runiq.AI.Workflows.Models;
using Runiq.AI.Agents;

namespace Runiq.AI.Workflows.Tests;

public sealed class FlowDefinitionValidatorTests
{
    /// <summary>
    /// En az bir bitis adimi olan geçerli workflow taniminin basarili dogrulandigini dogrular.
    /// </summary>
    [Fact]
    public void Validate_ShouldReturnSuccess_WhenFlowIsValid()
    {
        var workflow = new Flow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnSuccess("planner")
                .OnFailureStop()
            .Step<TestAgent>("planner")
                .OnSuccessEnd()
                .OnFailureStop()
            .Build();

        var result = FlowDefinitionValidator.Validate(workflow);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// Hiç adimi olmayan workflow taniminin geçersiz oldugunu dogrular.
    /// </summary>
    [Fact]
    public void Validate_ShouldReturnFailure_WhenFlowHasNoSteps()
    {
        var workflow = new Flow("empty", "Empty");

        var result = FlowDefinitionValidator.Validate(workflow);

        Assert.False(result.IsValid);
        Assert.Contains("Flow must contain at least one step.", result.Errors);
    }

    /// <summary>
    /// Ayni step id birden fazla kullanildiginda dogrulamanin hata döndürdügünü dogrular.
    /// </summary>
    [Fact]
    public void Validate_ShouldReturnFailure_WhenFlowHasDuplicateStepIds()
    {
        var workflow = new Flow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnSuccessEnd()
                .OnFailureStop()
            .Step<TestAgent>("weather")
                .OnSuccessEnd()
                .OnFailureStop()
            .Build();

        var result = FlowDefinitionValidator.Validate(workflow);

        Assert.False(result.IsValid);
        Assert.Contains("Flow contains duplicate step id 'weather'.", result.Errors);
    }

    /// <summary>
    /// Basari geçisi bilinmeyen bir adima isaret ettiginde dogrulamanin hata döndürdügünü dogrular.
    /// </summary>
    [Fact]
    public void Validate_ShouldReturnFailure_WhenSuccessTargetIsUnknown()
    {
        var workflow = new Flow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnSuccess("missing-step")
                .OnFailureStop()
            .Build();

        var result = FlowDefinitionValidator.Validate(workflow);

        Assert.False(result.IsValid);
        Assert.Contains("Step 'weather' has unknown success target 'missing-step'.", result.Errors);
    }

    /// <summary>
    /// Hata geçisi bilinmeyen bir adima isaret ettiginde dogrulamanin hata döndürdügünü dogrular.
    /// </summary>
    [Fact]
    public void Validate_ShouldReturnFailure_WhenFailureTargetIsUnknown()
    {
        var workflow = new Flow("travel", "Travel")
            .Step<TestAgent>("weather")
                .OnSuccessEnd()
                .OnFailureGoTo("missing-fallback")
            .Build();

        var result = FlowDefinitionValidator.Validate(workflow);

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
