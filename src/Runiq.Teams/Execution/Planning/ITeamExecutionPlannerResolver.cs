using Runiq.Teams.Models.Teams;

namespace Runiq.Teams.Execution.Planning;

/// <summary>
/// Agent team yürütme moduna uygun planlayıcıyı seçen bileşeni temsil eder.
/// </summary>
public interface ITeamExecutionPlannerResolver
{
    /// <summary>
    /// Verilen takım için kullanılacak execution planner örneğini çözümler.
    /// </summary>
    ITeamExecutionPlanner Resolve(AgentTeam team);
}
