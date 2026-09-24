using Runiq.AI.Agents.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Core;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class ClaudeLocalIntegrationTests
{
    [LocalClaudeFact]
    // Verifies real CLI authentication, JSONL execution and persisted session recall only when explicitly enabled.
    public async Task LocalCli_ResumesPersistedSession()
    {
        var workspace = Directory.CreateTempSubdirectory("runiq-claude-integration-");
        try
        {
            var collection = new ServiceCollection().AddLogging();
            collection.AddRuniqServer(options => options.AddAgent(new Agent("claude", "Claude",
                "Do not use tools, read files, change files, or run commands. Answer only the user's memory question.").UseClaude()));
            collection.Configure<ClaudeExecutorOptions>(options =>
            {
                options.WorkingDirectory = workspace.FullName;
                options.ExecutablePath = Environment.GetEnvironmentVariable("RUNIQ_CLAUDE_EXECUTABLE");
                options.Timeout = TimeSpan.FromMinutes(2);
            });
            await using var services = collection.BuildServiceProvider();
            await using var scope = services.CreateAsyncScope();
            var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
            var marker = "runiq-" + Guid.NewGuid().ToString("N");
            var first = await runtime.ExecuteAsync("claude", $"Remember this marker: {marker}. Reply with the marker only.");
            Assert.True(first.IsSuccess, $"{first.ErrorCode}: {first.ErrorMessage}");
            Assert.NotNull(first.ProviderSessionId);
            var followUp = await runtime.ExecuteAsync("claude", new AgentQuery("What marker did I ask you to remember? Reply with it only.")
                { ProviderSessionId = first.ProviderSessionId });
            Assert.True(followUp.IsSuccess, $"{followUp.ErrorCode}: {followUp.ErrorMessage}");
            Assert.Equal(first.ProviderSessionId, followUp.ProviderSessionId);
            Assert.NotEqual(first.RunId, followUp.RunId);
            Assert.Contains(marker, followUp.Message);
        }
        finally { workspace.Delete(recursive: true); }
    }

    private sealed class LocalClaudeFactAttribute : FactAttribute
    {
        public LocalClaudeFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("RUNIQ_CLAUDE_INTEGRATION") != "1")
                Skip = "Set RUNIQ_CLAUDE_INTEGRATION=1 to use an installed, authenticated local Claude CLI.";
        }
    }
}
