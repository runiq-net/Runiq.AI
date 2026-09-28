using Runiq.AI.Agents.Configuration;
using Runiq.AI.IncidentTriageAssistant.Agents;
using Runiq.AI.IncidentTriageAssistant.Tools;

namespace Runiq.AI.IncidentTriageAssistant.Tests;

public sealed class IncidentTriageAgentTests
{
    [Fact]
    // Verifies the sample exposes one Codex agent and one Claude agent with the expected executor kinds.
    public void AgentDefinitions_ShouldUseExpectedExecutors()
    {
        var codex = IncidentTriageAgents.CreateCodex();
        var claude = IncidentTriageAgents.CreateClaude();

        Assert.Equal("Incident Triage Assistant - Codex", codex.Name);
        Assert.Equal(AgentExecutorKind.Codex, codex.Executor?.Kind);
        Assert.Equal("gpt-5.6-sol", codex.Executor?.Codex?.Model);
        Assert.Equal("Incident Triage Assistant - Claude", claude.Name);
        Assert.Equal(AgentExecutorKind.Claude, claude.Executor?.Kind);
        Assert.Equal("sonnet", claude.Executor?.Claude?.Model);
    }

    [Fact]
    // Verifies both CLI agents share the exact same strongly typed incident tool registrations.
    public void AgentDefinitions_ShouldShareTheSameThreeTools()
    {
        var codex = IncidentTriageAgents.CreateCodex();
        var claude = IncidentTriageAgents.CreateClaude();

        var expected = new[]
        {
            ("get_service_metrics", typeof(GetServiceMetricsTool)),
            ("get_recent_deployments", typeof(GetRecentDeploymentsTool)),
            ("get_runbook", typeof(GetRunbookTool))
        };

        AssertTools(codex.Tools);
        AssertTools(claude.Tools);

        void AssertTools(IReadOnlyList<Runiq.AI.Agents.Tools.AgentToolRegistration> tools)
        {
            Assert.Equal(expected.Length, tools.Count);
            foreach (var (name, type) in expected)
            {
                var tool = Assert.Single(tools, candidate => candidate.Name == name);
                Assert.Equal(type, tool.ToolType);
            }
        }
    }
}
