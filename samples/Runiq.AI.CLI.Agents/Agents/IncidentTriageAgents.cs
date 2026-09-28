
using Runiq.AI.Agents;
using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Tools;
using Runiq.AI.CLI.Agents.Tools;

namespace Runiq.AI.CLI.Agents.Agents;

/// <summary>
/// Creates the Codex and Claude incident triage agents used by the sample.
/// </summary>
public static class IncidentTriageAgents
{
    private const string Instructions = """
        You are an incident triage assistant for a sample e-commerce backend.

        The scenario data is deterministic demo data. Use only the available tools and do not invent monitoring data,
        deployments, runbooks, production state, credentials, or cloud resources.

        Always inspect service metrics, recent deployments, and the runbook before producing the final answer.
        For this sample, request the runbook with incidentType "elevated-error-rate" unless the user explicitly asks for a different incident type.
        Treat deployment timing as correlation, not proof of root cause. Do not perform or recommend automatic rollback,
        production changes, or remediation actions that would modify a live system.
        Do not mention repository instructions, local skills, implementation details, or tool selection reasoning in the final answer.

        Start the final answer directly with the first heading. Do not include a preamble, planning note, or status update.
        Format the final answer with these exact headings:
        - Detected anomalies
        - Temporal relationship with deployment
        - Possible causes
        - Initial investigation steps
        - Sources used
        """;

    /// <summary>
    /// Creates the Codex CLI incident triage agent with the shared tool set.
    /// </summary>
    /// <returns>The configured Codex incident triage agent.</returns>
    public static Agent CreateCodex()
    {
        return new Agent(
            id: "incident-triage-codex",
            name: "Incident Triage Assistant - Codex",
            instructions: Instructions)
            .UseCodex(options =>
            {
                options.Model = "gpt-5.6-sol";
                options.ReasoningEffort = CodexReasoningEffort.Medium;
                options.ServiceTier = CodexServiceTier.Fast;
            })
            .AddIncidentTools();
    }

    /// <summary>
    /// Creates the Claude CLI incident triage agent with the shared tool set.
    /// </summary>
    /// <returns>The configured Claude incident triage agent.</returns>
    public static Agent CreateClaude()
    {
        return new Agent(
            id: "incident-triage-claude",
            name: "Incident Triage Assistant - Claude",
            instructions: Instructions)
            .UseClaude(options =>
            {
                options.Model = "sonnet";
                options.ReasoningEffort = ClaudeReasoningEffort.High;
            })
            .AddIncidentTools();
    }

    private static Agent AddIncidentTools(this Agent agent)
    {
        return agent
            .AddTool<GetServiceMetricsTool>()
            .AddTool<GetRecentDeploymentsTool>()
            .AddTool<GetRunbookTool>();
    }
}
