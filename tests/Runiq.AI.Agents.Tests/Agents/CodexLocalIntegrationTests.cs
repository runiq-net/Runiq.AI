using Runiq.AI.Agents.Configuration;
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
                "Do not use tools, read files, change files, or run commands. Answer only the user's memory question.")
                .UseCodex(options => options.Model = Environment.GetEnvironmentVariable("RUNIQ_CODEX_MODEL") ?? "gpt-6-sol")));
            collection.Configure<CodexExecutorOptions>(options =>
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

    [LocalCodexFact]
    // Verifies a deliberately unavailable model is normalized when an authenticated local CLI is explicitly enabled.
    public async Task LocalCli_InvalidModelReturnsMeaningfulFailure()
    {
        var collection = new ServiceCollection().AddLogging();
        collection.AddRuniqServer(options => options.AddAgent(new Agent("invalid", "Invalid", "Do not use tools.")
            .UseCodex(options => options.Model = "runiq-intentionally-invalid-model")));
        collection.Configure<CodexExecutorOptions>(options =>
        {
            options.WorkingDirectory = Path.GetTempPath();
            options.SkipGitRepositoryCheck = true;
            options.ExecutablePath = Environment.GetEnvironmentVariable("RUNIQ_CODEX_EXECUTABLE");
            options.Timeout = TimeSpan.FromMinutes(2);
        });
        await using var services = collection.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>().ExecuteAsync("invalid", "Say hello.");
        Assert.Equal("CodexModelNotAvailable", result.ErrorCode);
        Assert.Contains("runiq-intentionally-invalid-model", result.ErrorMessage!);
        Assert.Equal("runiq-intentionally-invalid-model", result.ErrorDetails!.RequestedModel);
        Assert.NotNull(result.ErrorDetails.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorDetails.DiagnosticDetail));
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
