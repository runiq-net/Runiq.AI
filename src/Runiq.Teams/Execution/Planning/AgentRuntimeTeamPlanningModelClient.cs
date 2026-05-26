using System.Text;
using System.Text.Json;
using Runiq.Agents;
using Runiq.Agents.Runtime;
using Runiq.Teams.Models.Teams;

namespace Runiq.Teams.Execution.Planning;

/// <summary>
/// Mevcut agent provider ayarlarını kullanarak adaptif team planlama model çağrısını yürütür.
/// </summary>
public sealed class AgentRuntimeTeamPlanningModelClient : ITeamPlanningModelClient
{
    private readonly AgentExecutionRuntime agentRuntime;
    private readonly IReadOnlyList<Agent> agents;

    /// <summary>
    /// Yeni bir agent runtime tabanlı team planning model istemcisi oluşturur.
    /// </summary>
    public AgentRuntimeTeamPlanningModelClient(
        AgentExecutionRuntime agentRuntime,
        IEnumerable<Agent> agents)
    {
        this.agentRuntime = agentRuntime;
        this.agents = agents.ToArray();
    }

    /// <inheritdoc />
    public async Task<string> CreatePlanJsonAsync(
        AgentTeam team,
        string userInput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        var sourceAgent = ResolveSourceAgent(team);
        var planningAgent = new Agent(
            id: $"team-planner-{team.Id}",
            name: $"{team.Name} Planner",
            instructions: BuildPlanningInstructions(),
            model: sourceAgent.Model,
            apiKey: sourceAgent.ApiKey,
            provider: sourceAgent.Provider,
            reasoningEffort: sourceAgent.ReasoningEffort,
            verbosity: "low");

        var input = BuildPlanningInput(team, userInput);
        var result = await agentRuntime.ExecuteAsync(
            planningAgent,
            input,
            cancellationToken);

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Message))
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Team planning model did not return a usable response.");
        }

        return result.Message;
    }

    private Agent ResolveSourceAgent(AgentTeam team)
    {
        for (var index = team.Members.Count - 1; index >= 0; index--)
        {
            var member = team.Members[index];
            var agent = agents.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, member.AgentId, StringComparison.OrdinalIgnoreCase));

            if (agent is not null)
            {
                return agent;
            }
        }

        throw new InvalidOperationException(
            $"Agent team '{team.Id}' does not have a registered member agent that can provide planning model settings.");
    }

    private static string BuildPlanningInstructions()
    {
        return """
        You are an execution planner for an agent team.

        Your only job is to choose which declared team members should run, in which order, for the user's request.
        Do not answer the user's request.
        Do not call tools.
        Do not invent agents.
        Do not use keyword routing. Reason about the request and the available member responsibilities.

        Return JSON only with this shape:
        {
          "planningSummary": "short summary",
          "steps": [
            {
              "agentId": "declared-agent-id",
              "reason": "why this member is useful"
            }
          ],
          "finalAgentId": "declared-agent-id"
        }

        The finalAgentId must be one of the selected steps.
        """;
    }

    private static string BuildPlanningInput(
        AgentTeam team,
        string userInput)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Team:");
        builder.AppendLine(JsonSerializer.Serialize(new
        {
            id = team.Id,
            name = team.Name,
            instructions = team.Instructions
        }));
        builder.AppendLine();
        builder.AppendLine("User request:");
        builder.AppendLine(userInput);
        builder.AppendLine();
        builder.AppendLine("Available team members:");

        foreach (var member in team.Members)
        {
            builder.AppendLine(JsonSerializer.Serialize(new
            {
                agentId = member.AgentId,
                role = member.Role,
                instructions = member.Instructions
            }));
        }

        return builder.ToString();
    }
}
