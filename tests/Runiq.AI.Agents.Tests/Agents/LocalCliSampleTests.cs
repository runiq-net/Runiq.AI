using Runiq.AI.Agents.Configuration;
using Runiq.AI.LocalCliAgents.Agents;
using Runiq.AI.LocalCliAgents.Tools;
using Runiq.AI.Core.Metadata;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class LocalCliSampleTests
{
    [Fact]
    // Verifies the documented prompt produces exact totals through the sample's actual deterministic tool.
    public async Task ChangeSummary_CalculatesDocumentedExample()
    {
        var result = await new ChangeSummaryTool().ExecuteAsync(new([
            new("OrderService.cs", 45, 12), new("OrderController.cs", 18, 4), new("OrderServiceTests.cs", 90, 0)]));
        Assert.Equal(new ChangeSummaryOutput(3, 153, 16), result);
        Assert.Equal(new ChangeSummaryOutput(0, 0, 0), await new ChangeSummaryTool().ExecuteAsync(new([])));
    }

    [Fact]
    // Verifies malformed, duplicate and negative inputs cannot produce misleading change totals.
    public async Task ChangeSummary_RejectsInvalidInputAndHonorsCancellation()
    {
        var tool = new ChangeSummaryTool();
        foreach (var input in new ChangeSummaryInput[] { new(null!), new([new("a", -1, 0)]), new([new("", 1, 0)]), new([new("a", 1, 0), new("a", 2, 0)]) })
            await Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteAsync(input));
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.ExecuteAsync(new([]), ct.Token));
    }

    [Fact]
    // Verifies the sample keeps Codex model settings independent and binds the shared tool to both CLI assistants.
    public void Agents_HaveDistinctSettingsAndToolBindings()
    {
        var quick = QuickProjectAssistant.Create();
        var reviewer = Runiq.AI.LocalCliAgents.Agents.CodexAgent.Create();
        Assert.NotEqual(quick.Executor!.Codex!.Model, reviewer.Executor!.Codex!.Model);
        Assert.Equal(CodexReasoningEffort.Medium, quick.Executor.Codex.ReasoningEffort);
        Assert.Equal(CodexServiceTier.Fast, quick.Executor.Codex.ServiceTier);
        Assert.Equal(CodexReasoningEffort.High, reviewer.Executor.Codex.ReasoningEffort);
        Assert.Equal(CodexServiceTier.Default, reviewer.Executor.Codex.ServiceTier);
        Assert.Equal("change_summary", Assert.Single(quick.Tools).Name);
        Assert.Empty(reviewer.Tools);
        var metadata = new RuntimeMetadataService([quick, reviewer]).GetAgents();
        Assert.All(metadata, item => Assert.Equal("Codex CLI", item.Provider));
        Assert.Equal(quick.Executor.Codex.Model, metadata[0].Model);
        Assert.Equal("change_summary", Assert.Single(metadata[0].Tools).Name);
        Assert.Equal(reviewer.Executor.Codex.Model, metadata[1].Model);
        Assert.Empty(metadata[1].Tools);
        var claude = ClaudeProjectAssistant.Create();
        Assert.Equal(AgentExecutorKind.Claude, claude.Executor!.Kind);
        Assert.Equal("claude-project-assistant", claude.Id);
        Assert.Equal("change_summary", Assert.Single(claude.Tools).Name);
    }
}
