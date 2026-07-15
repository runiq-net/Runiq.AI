using Runiq.AI.ContextSpaces.Models.Skills;
using Runiq.AI.ContextSpaces.Models.Sources;

namespace Runiq.AI.ContextSpaces.Services;

/// <summary>
/// Baglam alanina bagli skill kaynaklarindan skill tanimlarini kesfeder.
/// </summary>
public interface IContextSpaceSkillDiscoveryService
{
    /// <summary>
    /// Verilen baglam alanina bagli skill kaynaklarindan kesfedilen skill tanimlarini döner.
    /// </summary>
    IReadOnlyList<ContextSpaceSkill> Discover(ContextSpace contextSpace);
}
