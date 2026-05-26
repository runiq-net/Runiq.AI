using Runiq.Teams.Models.Teams;

namespace Runiq.Teams.Execution.Planning;

/// <summary>
/// Adaptif agent team planlaması için modelden ham JSON plan çıktısı alan istemciyi temsil eder.
/// </summary>
public interface ITeamPlanningModelClient
{
    /// <summary>
    /// Verilen takım ve kullanıcı girdisi için modelden JSON yürütme planı üretir.
    /// </summary>
    Task<string> CreatePlanJsonAsync(
        AgentTeam team,
        string userInput,
        CancellationToken cancellationToken = default);
}
