using System.Text.Json;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Core.AI.Chat;
using Runiq.AI.Memory.Configuration;
using Runiq.AI.Rag.Models.Documents;
using Runiq.AI.Rag.Models.Search;

namespace Runiq.AI.Agents.Tests.Runtime;

public sealed class AgentContextAssemblerTests
{
    [Fact]
    // Memory-only composition accounts for every message, tool schema and reserve exactly once under one policy.
    public void MemoryOnly_CountsAllComponentsAndAcceptsExactFit()
    {
        var agent = Create().UseMemory(new(history: new(2, 100)));
        ChatMessage[] history = [new(ChatRole.User, "old"), new(ChatRole.Assistant, "reply")];
        ChatMessage[] active = [new(ChatRole.User, "question"),
            new(ChatRole.Assistant, "working", ToolCalls: [new("call", "lookup", "{\"id\":1}")]),
            new(ChatRole.Tool, "{\"result\":true}", "call")];
        ChatToolDefinition[] tools = [new("lookup", "Looks up a record", "{\"type\":\"object\"}")];
        var probe = AgentContextAssembler.Assemble(agent, history, active, tools, new(), 1);
        var expected = probe.Messages.Sum(ContextTokenEstimator.EstimateMessage) + ContextTokenEstimator.EstimateTools(tools);
        Assert.Equal(expected, probe.Budget.EstimatedPromptTokens);
        Assert.True(probe.Budget.MandatoryPromptTokens > active.Sum(ContextTokenEstimator.EstimateMessage));
        agent.UseContextBudget(new(expected + 10, 10));
        var exact = AgentContextAssembler.Assemble(agent, history, active, tools, new(), 2);
        Assert.False(exact.Budget.MandatoryPromptOverflow);
        Assert.Equal(exact.Budget.MaximumContextTokens, exact.Budget.EstimatedPromptTokens + 10);
        Assert.Equal(2, exact.Budget.SelectedHistoryMessages);
        Assert.Equal(active, exact.Messages.TakeLast(3));
    }

    [Fact]
    // Mandatory content is never truncated; optional history is removed before an explanatory overflow is returned.
    public void MandatoryOverflow_ExcludesOptionalContent()
    {
        var agent = Create().UseMemory().UseContextBudget(new(12, 1));
        var assembly = AgentContextAssembler.Assemble(agent, [new(ChatRole.User, "history")],
            [new(ChatRole.User, "one two three")], [], new(), 1);
        Assert.True(assembly.Budget.MandatoryPromptOverflow);
        Assert.Equal(0, assembly.Budget.SelectedHistoryMessages);
        Assert.Equal("one two three", assembly.Messages[^1].Content);
        Assert.Equal(1, assembly.Budget.ExcludedHistoryMessages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // RAG configuration supplies the default window, while an explicit agent window consistently overrides it.
    public void EffectiveBudget_HasOnePrecedenceRule(bool withMemory)
    {
        var agent = Create().UseRag(options =>
        {
            options.IndexName = "index";
            options.ContextBudget.MaximumContextTokens = 2000;
            options.ContextBudget.ResponseTokenReserve = 100;
        });
        if (withMemory) agent.UseMemory();
        var original = Assemble(agent);
        Assert.Equal(2000, original.Budget.MaximumContextTokens);
        Assert.Equal(100, original.Budget.ResponseTokenReserve);
        agent.UseContextBudget(new(3000, 200));
        var overridden = Assemble(agent);
        Assert.Equal(3000, overridden.Budget.MaximumContextTokens);
        Assert.Equal(200, overridden.Budget.ResponseTokenReserve);
        Assert.Equal(withMemory, agent.Memory is not null);
    }

    [Fact]
    // Evidence has priority over history even when the recent claims look like numbered source citations.
    public void EvidenceWinsContention_AndHistoryNeverBecomesASource()
    {
        var agent = Create().UseMemory().UseRag(options =>
        {
            options.IndexName = "index";
            options.Mode = RagExecutionMode.Required;
            options.NoContextBehavior = RagNoContextBehavior.FailExecution;
        });
        var retrieval = new AgentRuntimeContext([Source("a", "evidence")]);
        ChatMessage[] history = [new(ChatRole.User, "claim [99]"), new(ChatRole.Assistant, "answer [1]")];
        var baseline = AgentContextAssembler.Assemble(agent, [], [new(ChatRole.User, "query")], [], retrieval, 1);
        agent.UseContextBudget(new((int)baseline.Budget.EstimatedPromptTokens + 10, 10));
        var result = AgentContextAssembler.Assemble(agent, history, [new(ChatRole.User, "query")], [], retrieval, 1);
        Assert.Single(result.Context.RetrievedRagContext);
        Assert.Equal(0, result.Budget.SelectedHistoryMessages);
        Assert.Equal(2, result.Budget.ExcludedHistoryMessages);
        var repeated = AgentContextAssembler.Assemble(agent, history, [new(ChatRole.User, "query")], [], retrieval, 1);
        Assert.Equal(result.Messages, repeated.Messages);
        Assert.Equal(result.Budget, repeated.Budget);
        Assert.Empty(AgentCitationProcessor.Validate("claim [99]", result.Context));
    }

    [Fact]
    // No fitting chunk keeps the existing exhaustion reason and recounts the actual no-context framework policy.
    public void EvidenceExhaustion_RecountsFallbackInstructions()
    {
        var agent = Create().UseMemory().UseRag(options => { options.IndexName = "index"; options.Mode = RagExecutionMode.Grounded; });
        var fallback = Assemble(agent);
        agent.UseContextBudget(new(fallback.Budget.MandatoryPromptTokens + 20, 10));
        var result = AgentContextAssembler.Assemble(agent, [new(ChatRole.Assistant, "claim")],
            [new(ChatRole.User, "query")], [], new([Source("a", string.Join(' ', Enumerable.Repeat("large", 200)))]), 1);
        Assert.False(result.Budget.MandatoryPromptOverflow);
        Assert.Equal(RagNoContextReason.ContextBudgetExhausted, result.Context.NoContextReason);
        Assert.Empty(result.Context.RetrievedRagContext);
        Assert.Contains("No document context", result.Messages[1].Content);
        Assert.Equal(result.Messages.Sum(ContextTokenEstimator.EstimateMessage), result.Budget.EstimatedPromptTokens);
        Assert.True(result.Budget.EstimatedPromptTokens + 10 <= result.Budget.MaximumContextTokens);
    }

    [Fact]
    // Configuration rejects invalid limits without enabling subsystems or selecting a persistence provider.
    public void Configuration_IsExplicitAndValidated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentContextBudgetOptions(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentContextBudgetOptions(10, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentContextBudgetOptions(10, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentContextBudgetOptions(10, 11));
        var agent = Create().UseContextBudget(new());
        Assert.Null(agent.Memory);
        Assert.Null(agent.Rag);
        Assert.Throws<InvalidOperationException>(() => agent.UseContextBudget(new()));
        var result = Assemble(agent);
        Assert.Equal(32768, result.Budget.MaximumContextTokens);
        Assert.Equal(4096, result.Budget.ResponseTokenReserve);
        Assert.Equal("EstimatedUnicodeRuns", result.Budget.AccountingMode);
    }

    [Fact]
    // Diagnostics expose accounting and exclusion counts without including prompt, evidence or tool content.
    public void Diagnostics_ContainOnlySafeCounts()
    {
        var agent = new Agent("agent", "Agent", "instruction-secret", "openai/model", "key").UseMemory();
        var result = AgentContextAssembler.Assemble(agent, [new(ChatRole.User, "history-secret")],
            [new(ChatRole.User, "query-secret"), new(ChatRole.Tool, "result-secret", "call-secret")],
            [new("tool-secret", "description-secret", "schema-secret")], new(), 4);
        var serialized = JsonSerializer.Serialize(result.Budget);
        Assert.DoesNotContain("secret", serialized);
        Assert.Contains("EstimatedUnicodeRuns", serialized);
        Assert.Equal(4, result.Budget.Invocation);
        Assert.Equal(result.Messages.Sum(ContextTokenEstimator.EstimateMessage) +
            ContextTokenEstimator.EstimateTools([new("tool-secret", "description-secret", "schema-secret")]), result.Budget.EstimatedPromptTokens);
    }

    [Fact]
    // Very large valid reserves still produce a budget failure rather than overflowing integer arithmetic.
    public void LargeReserve_ReportsMandatoryOverflow()
    {
        var agent = Create().UseContextBudget(new(int.MaxValue, int.MaxValue - 1));
        var result = Assemble(agent);
        Assert.True(result.Budget.MandatoryPromptOverflow);
        Assert.True(result.Budget.EstimatedPromptTokens + result.Budget.ResponseTokenReserve > int.MaxValue);
    }

    private static Agent Create() => new("agent", "Agent", "instructions", "openai/model", "key");
    private static AgentContextAssembly Assemble(Agent agent) => AgentContextAssembler.Assemble(agent, [], [new(ChatRole.User, "query")], [], new(), 1);
    internal static RagSearchResult Source(string id, string content) => new()
    {
        Chunk = new RagChunk { Id = id, DocumentId = "doc-" + id, Content = content },
        RawScore = 0.9, Relevance = 0.95, Metric = RagScoreMetrics.CosineSimilarity, HigherIsBetter = true
    };
}
