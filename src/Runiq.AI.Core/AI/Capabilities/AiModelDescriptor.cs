using Runiq.AI.Core.Models;

namespace Runiq.AI.Core.AI.Capabilities;

/// <summary>
/// Describes the effective, provider-neutral capabilities of a selected model.
/// </summary>
/// <param name="Model">The provider and model identity.</param>
/// <param name="Capabilities">The explicitly resolved capabilities. No capability is implied by another.</param>
/// <param name="EmbeddingDimensions">The fixed embedding dimension count when known; otherwise null.</param>
public sealed record AiModelDescriptor(
    ModelReference Model,
    ModelCapability Capabilities,
    int? EmbeddingDimensions = null)
{
    /// <summary>Determines whether the model supports a particular operation.</summary>
    /// <param name="capability">The capability to test.</param>
    /// <returns>True only when every requested capability is declared.</returns>
    public bool Supports(ModelCapability capability) =>
        capability != ModelCapability.None && (Capabilities & capability) == capability;
}
