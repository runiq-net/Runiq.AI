using Runiq.AI.Core.Models;

namespace Runiq.AI.Core.AI.Capabilities;

/// <summary>
/// Resolves conservative defaults for built-in provider protocols. It intentionally does not maintain a model catalog.
/// </summary>
public sealed class DefaultModelCapabilityResolver : IModelCapabilityResolver
{
    /// <inheritdoc />
    public AiModelDescriptor Resolve(ModelReference model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var capabilities = model.Capabilities ?? (string.Equals(model.ProviderName, "openai", StringComparison.OrdinalIgnoreCase)
            ? ModelCapability.Chat | ModelCapability.Streaming | ModelCapability.ToolCalling | ModelCapability.StructuredOutput
            : ModelCapability.Chat | ModelCapability.Streaming);

        return new AiModelDescriptor(model, capabilities, model.EmbeddingDimensions);
    }
}
