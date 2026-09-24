using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Core;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class CodexLocalIntegrationTests
{
    [LocalCodexFact]
    // Verifies real CLI authentication, JSONL execution and persisted session recall only when explicitly enabled.
    public async Task LocalCli_ResumesPersistedSession()
    {
        var workspace = Directory.CreateTempSubdirectory("runiq-codex-integration-");
        try
        {
            var collection = new ServiceCollection().AddLogging();
            collection.AddRuniqServer(options => options.AddAgent(new Agent("codex", "Codex",
                "Do not use tools, read files, change files, or run commands. Answer only the user's memory question.").UseCodex()));
            collection.AddRuniqCodexExecutor(options =>
            {
                options.WorkingDirectory = workspace.FullName;
                options.SkipGitRepositoryCheck = true;
                options.ExecutablePath = Environment.GetEnvironmentVariable("RUNIQ_CODEX_EXECUTABLE");
                options.Timeout = TimeSpan.FromMinutes(2);
            });
            await using var services = collection.BuildServiceProvider();
            await using var scope = services.CreateAsyncScope();
            var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
            var marker = "runiq-" + Guid.NewGuid().ToString("N");
            var first = await runtime.ExecuteAsync("codex", $"Remember this marker: {marker}. Reply with the marker only.");
            Assert.True(first.IsSuccess, $"{first.ErrorCode}: {first.ErrorMessage}");
            Assert.NotNull(first.ProviderSessionId);
            var followUp = await runtime.ExecuteAsync("codex", new AgentQuery("What marker did I ask you to remember? Reply with it only.")
                { ProviderSessionId = first.ProviderSessionId });
            Assert.True(followUp.IsSuccess, $"{followUp.ErrorCode}: {followUp.ErrorMessage}");
            Assert.Equal(first.ProviderSessionId, followUp.ProviderSessionId);
            Assert.NotEqual(first.RunId, followUp.RunId);
            Assert.Contains(marker, followUp.Message);
        }
        finally { workspace.Delete(recursive: true); }
    }

    private sealed class LocalCodexFactAttribute : FactAttribute
    {
        public LocalCodexFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("RUNIQ_CODEX_INTEGRATION") != "1")
                Skip = "Set RUNIQ_CODEX_INTEGRATION=1 to use an installed, authenticated local Codex CLI.";
        }
    }
}
