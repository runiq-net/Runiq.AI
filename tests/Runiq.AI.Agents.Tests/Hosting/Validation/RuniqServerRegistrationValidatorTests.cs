using Runiq.AI.Agents;
using Runiq.AI.ContextSpaces.Models.Sources;
using Runiq.AI.Core.Configuration;

namespace Runiq.AI.Core.Validation;

/// <summary>
/// Runiq server kayitlarinin projeler arasi bütünlügünü dogrular.
/// </summary>
internal static class RuniqServerRegistrationValidatorTests
{
    /// <summary>
    /// Agent, tool ve context space kayitlarinin birlikte tutarli olup olmadigini dogrular.
    /// </summary>
    /// <param name="options">Host uygulama tarafindan yapilandirilan Runiq server seçenekleri.</param>
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
