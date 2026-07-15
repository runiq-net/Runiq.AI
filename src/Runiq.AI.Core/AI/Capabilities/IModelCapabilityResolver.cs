using Runiq.AI.Core.Models;

namespace Runiq.AI.Core.AI.Capabilities;

/// <summary>
/// Resolves effective model capabilities from the selected provider and model identity.
/// Implementations must be thread-safe when registered as shared services.
/// </summary>
public interface IModelCapabilityResolver
{
    /// <summary>Resolves a descriptor for a model.</summary>
    /// <param name="model">The provider and model to resolve.</param>
    /// <returns>The effective descriptor. Unknown model-specific features are not claimed.</returns>
    AiModelDescriptor Resolve(ModelReference model);
}
