using System.Text.Json;
using System.Text.Json.Serialization;
using Runiq.Teams.Models.Execution;
using Runiq.Teams.Models.Teams;

namespace Runiq.Teams.Execution.Planning;

/// <summary>
/// Kullanıcı isteğine göre takım üyelerini model destekli olarak seçen adaptif yürütme planlayıcısıdır.
/// </summary>
public sealed class AdaptiveTeamExecutionPlanner : ITeamExecutionPlanner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITeamPlanningModelClient modelClient;
    private readonly SequentialTeamExecutionPlanner fallbackPlanner;

    /// <summary>
    /// Yeni bir adaptif team execution planner örneği oluşturur.
    /// </summary>
    public AdaptiveTeamExecutionPlanner(
        ITeamPlanningModelClient modelClient,
        SequentialTeamExecutionPlanner fallbackPlanner)
    {
        this.modelClient = modelClient;
        this.fallbackPlanner = fallbackPlanner;
    }

    /// <inheritdoc />
    public async Task<TeamExecutionPlan> CreatePlanAsync(
        AgentTeam team,
        string userInput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        try
        {
            var json = await modelClient.CreatePlanJsonAsync(
                team,
                userInput,
                cancellationToken);

            var plan = TryCreateValidatedPlan(team, json);

            return plan ?? await CreateFallbackPlanAsync(
                team,
                userInput,
                "Adaptive planning returned no valid team members and sequential fallback was used.",
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return await CreateFallbackPlanAsync(
                team,
                userInput,
                $"Adaptive planning failed and sequential fallback was used: {exception.Message}",
                cancellationToken);
        }
    }

    private TeamExecutionPlan? TryCreateValidatedPlan(
        AgentTeam team,
        string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var response = JsonSerializer.Deserialize<AdaptivePlannerResponse>(
            ExtractJsonObject(json),
            JsonOptions);

        if (response?.Steps is null || response.Steps.Count == 0)
        {
            return null;
        }

        var membersById = team.Members.ToDictionary(
            member => member.AgentId,
            StringComparer.OrdinalIgnoreCase);
        var selectedSteps = new List<(TeamMember Member, string Reason)>();
        var seenAgentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var responseStep in response.Steps)
        {
            if (string.IsNullOrWhiteSpace(responseStep.AgentId))
            {
                continue;
            }

            if (!membersById.TryGetValue(responseStep.AgentId.Trim(), out var member))
            {
                continue;
            }

            if (!seenAgentIds.Add(member.AgentId))
            {
                continue;
            }

            selectedSteps.Add((
                member,
                string.IsNullOrWhiteSpace(responseStep.Reason)
                    ? "Selected by adaptive team planning."
                    : responseStep.Reason.Trim()));
        }

        if (selectedSteps.Count == 0)
        {
            return null;
        }

        var finalAgentId = ResolveFinalAgentId(
            response.FinalAgentId,
            selectedSteps);

        var planSteps = selectedSteps
            .Select((step, index) => new TeamExecutionPlanStep(
                agentId: step.Member.AgentId,
                role: step.Member.Role,
                reason: step.Reason,
                order: index,
                isFinalMember: string.Equals(
                    step.Member.AgentId,
                    finalAgentId,
                    StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return new TeamExecutionPlan(
            planSteps,
            finalAgentId,
            response.PlanningSummary);
    }

    private static string ResolveFinalAgentId(
        string? finalAgentId,
        IReadOnlyList<(TeamMember Member, string Reason)> selectedSteps)
    {
        if (!string.IsNullOrWhiteSpace(finalAgentId) &&
            selectedSteps.Any(step =>
                string.Equals(step.Member.AgentId, finalAgentId.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return finalAgentId.Trim();
        }

        return selectedSteps[^1].Member.AgentId;
    }

    private async Task<TeamExecutionPlan> CreateFallbackPlanAsync(
        AgentTeam team,
        string userInput,
        string planningSummary,
        CancellationToken cancellationToken)
    {
        var fallbackPlan = await fallbackPlanner.CreatePlanAsync(
            team,
            userInput,
            cancellationToken);

        return new TeamExecutionPlan(
            fallbackPlan.Steps,
            fallbackPlan.FinalAgentId,
            planningSummary);
    }

    private static string ExtractJsonObject(string value)
    {
        var trimmed = value.Trim();
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');

        if (firstBrace < 0 || lastBrace < firstBrace)
        {
            return trimmed;
        }

        return trimmed[firstBrace..(lastBrace + 1)];
    }

    private sealed class AdaptivePlannerResponse
    {
        public string? PlanningSummary { get; set; }

        public IReadOnlyList<AdaptivePlannerStep>? Steps { get; set; }

        public string? FinalAgentId { get; set; }
    }

    private sealed class AdaptivePlannerStep
    {
        public string? AgentId { get; set; }

        public string? Reason { get; set; }
    }
}
