using Runiq.Teams.Models.Execution;
using Runiq.Teams.Models.Teams;

namespace Runiq.Teams.Execution.Planning;

/// <summary>
/// Agent team üyelerini tanımlandıkları sırayla çalıştıran yürütme planını oluşturur.
/// </summary>
public sealed class SequentialTeamExecutionPlanner : ITeamExecutionPlanner
{
    private const string StepReason =
        "Selected because this member is part of the sequential team definition.";

    /// <inheritdoc />
    public Task<TeamExecutionPlan> CreatePlanAsync(
        AgentTeam team,
        string userInput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        if (team.Members.Count == 0)
        {
            throw new InvalidOperationException(
                $"Agent team '{team.Id}' does not have any members.");
        }

        var finalMember = team.Members[^1];
        var steps = team.Members
            .Select((member, index) => new TeamExecutionPlanStep(
                agentId: member.AgentId,
                role: member.Role,
                reason: StepReason,
                order: index,
                isFinalMember: index == team.Members.Count - 1))
            .ToArray();

        var plan = new TeamExecutionPlan(
            steps,
            finalMember.AgentId,
            "Sequential team plan generated from declared member order.");

        return Task.FromResult(plan);
    }
}
