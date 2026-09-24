using Runiq.AI.Agents;
using Runiq.AI.Agents.Tools;
using Runiq.AI.LocalCliAgents.Tools;

namespace Runiq.AI.LocalCliAgents.Agents;

/// <summary>Defines a Claude CLI assistant with a deterministic change-summary tool.</summary>
public static class ClaudeProjectAssistant
{
    /// <summary>Creates the assistant using the local Claude CLI configuration and authentication.</summary>
    /// <returns>A Claude agent with the change-summary tool attached.</returns>
    public static Agent Create() => new Agent("claude-project-assistant", "ClaudeProjectAssistant", """
        You are a concise project assistant. Answer in the user's language.
        For file change totals, always call the Runiq change_summary tool with the
        supplied file names, additions and deletions. Do not calculate totals yourself
        or substitute a shell command. Ask for missing input instead of guessing.
        Explain the returned totals briefly. Never claim a tool succeeded if it failed.
        Never modify files, commit or push. Each request is self-contained.
        """)
        .UseClaude()
        .AddTool<ChangeSummaryTool>();
}
