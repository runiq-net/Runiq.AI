using Runiq.Agents;
using Runiq.ContextSpaces.Models.Sources;
using Runiq.Core.Configuration;

namespace Runiq.Core.Validation;

/// <summary>
/// Runiq server kayıtlarının projeler arası bütünlüğünü doğrular.
/// </summary>
internal static class RuniqServerRegistrationValidator
{
    /// <summary>
    /// Agent, tool ve context space kayıtlarının birlikte tutarlı olup olmadığını doğrular.
    /// </summary>
    /// <param name="options">Host uygulama tarafından yapılandırılan Runiq server seçenekleri.</param>
    public static void Validate(RuniqServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidateAgentContextSpaces(
            options.Agents,
            options.ContextSpaces);
    }

    private static void ValidateAgentContextSpaces(
        IEnumerable<Agent> agents,
        IEnumerable<ContextSpace> contextSpaces)
    {
        var contextSpaceIds = contextSpaces
            .Select(contextSpace => contextSpace.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var agent in agents)
        {
            foreach (var contextSpaceId in agent.ContextSpaceIds)
            {
                if (contextSpaceIds.Contains(contextSpaceId))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Runiq server registration failed. Agent '{agent.Id}' uses unknown context space '{contextSpaceId}'.");
            }
        }
    }
}