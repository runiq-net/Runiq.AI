using Runiq.AI.Agents.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Agents.Runtime;
using Runiq.AI.Agents.Tools;
using Runiq.AI.Core;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class ClaudeLocalIntegrationTests
{
    [LocalClaudeFact]
    // Verifies the real CLI invokes a Runiq tool and reconnects on resume when an authenticated local account is explicitly enabled.
    public async Task LocalCli_InvokesRuniqToolAndResumes()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddRuniqServer(o => o.AddAgent(new Agent("tools", "Tools", "Always use change_summary for change counts.")
            .UseClaude().AddTool<Runiq.AI.LocalCliAgents.Tools.ChangeSummaryTool>()));
        services.Configure<ClaudeExecutorOptions>(o =>
        {
            o.WorkingDirectory = Path.GetTempPath();
            o.ExecutablePath = Environment.GetEnvironmentVariable("RUNIQ_CLAUDE_EXECUTABLE");
            o.Timeout = TimeSpan.FromMinutes(2);
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<AgentExecutionRuntime>();
        string? session = null;
        for (var turn = 0; turn < 2; turn++)
        {
            var events = new List<AgentExecutionEvent>();
            await foreach (var item in runtime.ExecuteStreamAsync("tools", new AgentQuery(
                "Call change_summary for a.cs +45/-12 and b.cs +18/-4. Report its totals. Do not use other tools.")
                { ProviderSessionId = session })) events.Add(item);
            Assert.True(events[^1].Kind == AgentExecutionEventKind.Completed, events[^1].ErrorMessage);
            Assert.Contains(events, e => e.Kind == AgentExecutionEventKind.ToolCallCompleted && e.ToolName == "change_summary" && e.OutputJson!.Contains("63"));
            Assert.NotNull(events[^1].ProviderSessionId);
            if (session is not null) Assert.Equal(session, events[^1].ProviderSessionId);
            session = events[^1].ProviderSessionId;
        }
    }
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
