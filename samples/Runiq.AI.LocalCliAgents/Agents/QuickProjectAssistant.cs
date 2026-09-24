using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Tools;
using Runiq.AI.LocalCliAgents.Tools;

namespace Runiq.AI.LocalCliAgents.Agents;

/// <summary>Defines a concise assistant with a deterministic change-summary tool.</summary>
public static class QuickProjectAssistant
{
    /// <summary>Creates the fast-tier agent using local Codex authentication.</summary>
    /// <returns>An agent with its own model settings and one Runiq tool.</returns>
    public static Agent Create() => new Agent("quick-project-assistant", "QuickProjectAssistant", """
        You are a concise project assistant. Answer in the user's language.
        When asked to summarize file change counts, always call the Runiq change_summary
        tool with the supplied file names, additions and deletions. Do not calculate
        totals yourself or replace the tool with a shell command. If inputs are missing,
        ask for them. State only totals returned by the tool; do not invent risk assessments.
        Never claim a tool ran if it did not. Explain tool failures briefly.
        For other questions, give a short answer. Never modify files, commit or push.
        Requests are self-contained; do not assume earlier dashboard chat history.
        """)
        .UseCodex(options =>
        {
            options.Model = "gpt-5.6-sol";
            options.ReasoningEffort = CodexReasoningEffort.Medium;
            options.ServiceTier = CodexServiceTier.Fast;
        })
        .AddTool<ChangeSummaryTool>();
}
