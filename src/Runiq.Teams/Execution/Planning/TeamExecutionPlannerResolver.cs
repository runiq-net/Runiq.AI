using Runiq.Teams.Models.Teams;

namespace Runiq.Teams.Execution.Planning;

/// <summary>
/// Takım yürütme moduna göre sıralı veya adaptif execution planner seçer.
/// </summary>
public sealed class TeamExecutionPlannerResolver : ITeamExecutionPlannerResolver
{
    private readonly SequentialTeamExecutionPlanner sequentialPlanner;
    private readonly AdaptiveTeamExecutionPlanner adaptivePlanner;

    /// <summary>
    /// Yeni bir team execution planner resolver örneği oluşturur.
    /// </summary>
    public TeamExecutionPlannerResolver(
        SequentialTeamExecutionPlanner sequentialPlanner,
        AdaptiveTeamExecutionPlanner adaptivePlanner)
    {
        this.sequentialPlanner = sequentialPlanner;
        this.adaptivePlanner = adaptivePlanner;
    }

    /// <inheritdoc />
    public ITeamExecutionPlanner Resolve(AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(team);

        return team.ExecutionMode switch
        {
            TeamExecutionMode.Sequential => sequentialPlanner,
            TeamExecutionMode.Adaptive => adaptivePlanner,
            _ => throw new NotSupportedException(
                $"Team execution mode '{team.ExecutionMode}' is not supported.")
        };
    }
}
