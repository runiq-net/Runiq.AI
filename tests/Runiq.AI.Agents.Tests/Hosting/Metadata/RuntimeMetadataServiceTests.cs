using Runiq.AI.Agents.Configuration;
using Runiq.AI.Core.Metadata;

namespace Runiq.AI.Agents.Tests.Hosting.Metadata;

public sealed class RuntimeMetadataServiceTests
{
    [Fact]
    // Verifies Claude metadata exposes its explicit model and reasoning settings without provider credentials.
    public void GetAgents_Claude_ProjectsProviderAndAgentModel()
    {
        var agent = new Agent("claude", "Claude", "instructions").UseClaude(claude => claude.Model = "sonnet");
        var metadata = Assert.Single(new RuntimeMetadataService([agent]).GetAgents());
        Assert.Equal("Claude CLI", metadata.Provider);
        Assert.Equal("sonnet", metadata.Model);
        Assert.Equal("high", metadata.ReasoningEffort);
        Assert.Null(metadata.Verbosity);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(metadata,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal("sonnet", json.GetProperty("model").GetString());
        Assert.Equal("high", json.GetProperty("reasoningEffort").GetString());
    }

    [Theory]
    [InlineData("gpt-6-sol", CodexReasoningEffort.High, "high")]
    [InlineData("custom/model-name", CodexReasoningEffort.Medium, "medium")]
    // Verifies Codex dashboard metadata preserves explicit model names and distinguishes the CLI from model providers.
    public void GetAgents_Codex_ProjectsProviderAndAgentModel(string model, CodexReasoningEffort effort, string expectedEffort)
    {
        var agent = new Agent("codex", "Codex", "instructions").UseCodex(options =>
        {
            options.Model = model;
            options.ReasoningEffort = effort;
        });
        var metadata = Assert.Single(new RuntimeMetadataService([agent]).GetAgents());
        Assert.Equal("Codex CLI", metadata.Provider);
        Assert.Equal(model, metadata.Model);
        Assert.Equal(expectedEffort, metadata.ReasoningEffort);
        Assert.Null(metadata.Verbosity);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(metadata,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal("Codex CLI", json.GetProperty("provider").GetString());
        Assert.Equal(model, json.GetProperty("model").GetString());
    }

    // Proves agent metadata exposes the effective reranking settings consumed by Running Behavior.
    [Fact]
    public void GetAgents_RerankingConfigured_ProjectsRunningBehaviorMetadata()
    {
        var agent = new Agent("agent", "Agent", "instructions", "openai/model", "key")
            .UseRag(options =>
            {
                options.IndexName = "documents";
                options.Reranking.Enabled = true;
                options.Reranking.MaximumCandidates = 8;
                options.Reranking.Timeout = TimeSpan.FromSeconds(3.5);
                options.Reranking.FailurePolicy = RagRerankerFailurePolicy.Fail;
            });
        var service = new RuntimeMetadataService([agent]);

        var reranking = Assert.Single(service.GetAgents()).Rag.Reranking;

        Assert.True(reranking.Enabled);
        Assert.Equal(8, reranking.MaximumCandidates);
        Assert.Equal(TimeSpan.FromSeconds(3.5), reranking.Timeout);
        Assert.Equal("Fail", reranking.FailurePolicy);
    }
}
