using Runiq.Teams.Models.Execution;
using Runiq.Teams.Models.Teams;

namespace Runiq.Teams.Execution.Planning;

/// <summary>
/// Agent team için kullanıcı girdisine göre yürütme planı oluşturan bileşeni temsil eder.
/// </summary>
public interface ITeamExecutionPlanner
{
    /// <summary>
    /// Verilen takım ve kullanıcı girdisi için yürütme planı oluşturur.
    /// </summary>
    Task<TeamExecutionPlan> CreatePlanAsync(
        AgentTeam team,
        string userInput,
        CancellationToken cancellationToken = default);
}
