using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime.Claude;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class ClaudeConfigurationTests
{
    [Fact]
    // Verifies selection requires an explicit callback and model instead of silently inheriting local defaults.
    public void UseClaude_RequiresConfigurationArgument()
    {
        var method = Assert.Single(typeof(Agent).GetMethods(), method => method.Name == "UseClaude");
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(Action<ClaudeAgentOptions>), parameter.ParameterType);
        Assert.False(parameter.IsOptional);
        var agent = new Agent("a", "A", "");
        Assert.Throws<ArgumentException>(() => agent.UseClaude(null!));
        Assert.Throws<ArgumentException>(() => agent.UseClaude(_ => { }));
        Assert.Null(agent.Executor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    // Verifies invalid models leave selection available and report the original setting name and agent identity.
    public void UseClaude_RejectsBlankModelAndAllowsCorrection(string? model)
    {
        var agent = new Agent("a", "A", "");
        var failure = Assert.Throws<ArgumentException>(() => agent.UseClaude(options => options.Model = model!));
        Assert.Equal("options.Model", failure.ParamName);
        Assert.Contains("Agent 'a'", failure.Message);
        Assert.Null(agent.Executor);
        agent.UseClaude(options => options.Model = " sonnet ");
        Assert.Equal("sonnet", agent.Executor!.Claude!.Model);
    }

    [Fact]
    // Verifies undefined effort cannot publish a configuration, and validation failure permits another executor.
    public void UseClaude_RejectsUndefinedEffort()
    {
        var agent = new Agent("a", "A", "");
        var failure = Assert.Throws<ArgumentException>(() => agent.UseClaude(options =>
        {
            options.Model = "sonnet";
            options.ReasoningEffort = (ClaudeReasoningEffort)999;
        }));
        Assert.Equal("ReasoningEffort", failure.ParamName);
        Assert.Null(agent.Executor);
        agent.UseModel("openai/model");
        Assert.Equal(AgentExecutorKind.Model, agent.Executor!.Kind);
    }

    [Fact]
    // Verifies immutable settings, independent agents and rejection of any second selection before its callback runs.
    public void UseClaude_SnapshotsIndependentOptions()
    {
        ClaudeAgentOptions? captured = null;
        var first = new Agent("a", "A", "").UseClaude(options => { captured = options; options.Model = "sonnet"; });
        captured!.Model = "changed";
        captured.ReasoningEffort = ClaudeReasoningEffort.Low;
        var second = new Agent("b", "B", "").UseClaude(options =>
        {
            options.Model = "opus";
            options.ReasoningEffort = ClaudeReasoningEffort.Medium;
        });
        Assert.Equal("sonnet", first.Executor!.Claude!.Model);
        Assert.Equal(ClaudeReasoningEffort.High, first.Executor.Claude.ReasoningEffort);
        Assert.Equal("opus", second.Executor!.Claude!.Model);
        Assert.Equal(ClaudeReasoningEffort.Medium, second.Executor.Claude.ReasoningEffort);
        var invoked = false;
        Assert.Throws<InvalidOperationException>(() => first.UseClaude(_ => invoked = true));
        Assert.False(invoked);
        Assert.Null(first.Executor.Model);
        Assert.Null(first.Executor.Codex);
    }

    [Theory]
    [InlineData(ClaudeReasoningEffort.Low, null)]
    [InlineData(ClaudeReasoningEffort.Medium, "0199a213-81c0-7800-8aa1-bbab2a035a53")]
    [InlineData(ClaudeReasoningEffort.High, null)]
    [InlineData(ClaudeReasoningEffort.XHigh, "0199a213-81c0-7800-8aa1-bbab2a035a53")]
    [InlineData(ClaudeReasoningEffort.Max, null)]
    // Verifies model arguments remain literal, effort is explicit, and resumed turns preserve the agent's settings.
    public void Command_MapsAgentOptions(ClaudeReasoningEffort effort, string? session)
    {
        var agent = new Agent("a", "A", "").UseClaude(options =>
        {
            options.Model = "future model --not-an-option";
            options.ReasoningEffort = effort;
        });
        var host = new ClaudeExecutorOptions { WorkingDirectory = Path.GetTempPath(), ExecutablePath = Environment.ProcessPath };
        var command = ClaudeCommand.Create(host, agent.Executor!.Claude!, session);
        Assert.Equal("future model --not-an-option", command.ArgumentList[command.ArgumentList.IndexOf("--model") + 1]);
        Assert.Equal(effort.ToString().ToLowerInvariant(), command.ArgumentList[command.ArgumentList.IndexOf("--effort") + 1]);
        Assert.False(command.Environment.ContainsKey("CLAUDE_CODE_EFFORT_LEVEL"));
        Assert.Equal(session is not null, command.ArgumentList.Contains("--resume"));
        if (session is not null) Assert.Equal(session, command.ArgumentList.Last());
        Assert.DoesNotContain(typeof(ClaudeExecutorOptions).GetProperties(), property =>
            property.Name is "Model" or "ReasoningEffort" or "ServiceTier");
    }
}
