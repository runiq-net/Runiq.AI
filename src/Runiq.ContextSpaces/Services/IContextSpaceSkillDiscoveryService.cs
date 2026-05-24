using Runiq.ContextSpaces.Models.Skills;
using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.ContextSpaces.Services;

/// <summary>
/// Bağlam alanına bağlı skill kaynaklarından skill tanımlarını keşfeder.
/// </summary>
public interface IContextSpaceSkillDiscoveryService
{
    /// <summary>
    /// Verilen bağlam alanına bağlı skill kaynaklarından keşfedilen skill tanımlarını döner.
    /// </summary>
    IReadOnlyList<ContextSpaceSkill> Discover(ContextSpace contextSpace);
}