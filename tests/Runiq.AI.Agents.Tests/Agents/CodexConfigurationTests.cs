using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime.Codex;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class CodexConfigurationTests
{
    [Fact]
    // Verifies the public API cannot select Codex without supplying a configuration callback.
    public void UseCodex_RequiresConfigurationArgument()
    {
        var method = Assert.Single(typeof(Agent).GetMethods(), method => method.Name == "UseCodex");
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(Action<CodexAgentOptions>), parameter.ParameterType);
        Assert.False(parameter.IsOptional);
        Assert.Throws<ArgumentException>(() => new Agent("a", "A", "").UseCodex(null!));
        Assert.Throws<ArgumentException>(() => new Agent("a", "A", "").UseCodex(_ => { }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    // Verifies invalid models fail at definition time and leave the agent available for a valid selection.
    public void UseCodex_RejectsBlankModel(string? model)
    {
        var agent = new Agent("a", "A", "");
        Assert.Throws<ArgumentException>(() => agent.UseCodex(options => options.Model = model!));
        Assert.Null(agent.Executor);
        agent.UseCodex(options => options.Model = "future-model");
        Assert.Equal("future-model", agent.Executor!.Codex!.Model);
    }

    [Fact]
    // Verifies defaults and immutable per-agent snapshots without maintaining a model catalog.
    public void UseCodex_SnapshotsIndependentOptions()
    {
        CodexAgentOptions? captured = null;
        var agent = new Agent("a", "A", "").UseCodex(options => { captured = options; options.Model = "abcd"; });
        Assert.Equal(CodexReasoningEffort.High, agent.Executor!.Codex!.ReasoningEffort);
        Assert.Equal(CodexServiceTier.Default, agent.Executor.Codex.ServiceTier);
        captured!.Model = "changed";
        captured.ReasoningEffort = CodexReasoningEffort.Low;
        captured.ServiceTier = CodexServiceTier.Fast;
        Assert.Equal("abcd", agent.Executor.Codex.Model);
        Assert.Equal(CodexReasoningEffort.High, agent.Executor.Codex.ReasoningEffort);
        Assert.Equal(CodexServiceTier.Default, agent.Executor.Codex.ServiceTier);
        Assert.Throws<InvalidOperationException>(() => agent.UseCodex(options => options.Model = "different"));
    }

    [Theory]
    [InlineData(CodexReasoningEffort.High, CodexServiceTier.Default, null)]
    [InlineData(CodexReasoningEffort.Medium, CodexServiceTier.Fast, null)]
    [InlineData(CodexReasoningEffort.XHigh, CodexServiceTier.Fast, "0199a213-81c0-7800-8aa1-bbab2a035a53")]
    // Verifies explicit model and typed overrides are mapped identically for new and resumed turns.
    public void Command_MapsAgentOptions(CodexReasoningEffort effort, CodexServiceTier tier, string? session)
    {
        var agent = new Agent("a", "A", "").UseCodex(options =>
        {
            options.Model = "future model --not-an-option";
            options.ReasoningEffort = effort;
            options.ServiceTier = tier;
        });
        var host = new CodexExecutorOptions { WorkingDirectory = Path.GetTempPath(), ExecutablePath = Environment.ProcessPath };
        var command = CodexCommand.Create(host, agent.Executor!.Codex!, session);
        Assert.Equal("future model --not-an-option", command.ArgumentList[command.ArgumentList.IndexOf("--model") + 1]);
        Assert.Contains($"model_reasoning_effort=\"{effort.ToString().ToLowerInvariant()}\"", command.ArgumentList);
        Assert.Equal(tier == CodexServiceTier.Fast, command.ArgumentList.Contains("service_tier=\"fast\""));
        Assert.Equal(session is not null, command.ArgumentList.Contains("resume"));
        Assert.DoesNotContain(typeof(CodexExecutorOptions).GetProperties(), property =>
            property.Name is "Model" or "ReasoningEffort" or "ServiceTier");
    }
}
